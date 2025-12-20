using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;

namespace SwiftPage.Web.Pages.Docs.Components
{
    public partial class DataUpload : ComponentBase
    {
        // =============================
        // ========== DI / JS ==========
        // =============================

        [Inject] private IJSRuntime JS { get; set; } = default!;

        // =============================
        // ========= Constants =========
        // =============================

        private int _activeStep = 0;

        private const int MaxUploadFileSizeBytes = 10 * 1024 * 1024; // 10MB
        private const string CsvMimeType = "text/csv;charset=utf-8;";
        private const string TemplateFileName = "employee-template.csv";
        private const string ExportFileName = "employees-export.csv";

        private static readonly string[] EnglishHeaderTemplate = ["Name", "Department", "JoinDate"];
        private static readonly string[] KoreanHeaderTemplate  = ["이름", "부서", "입사일"];

        private const int RequiredHeaderCount = 3;

        private static class ImportStatus
        {
            public const string Failure = "실패";
        }

        // =============================
        // ========= Entities ==========
        // =============================

        public class Employee
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Department { get; set; }
            public DateTime JoinDate { get; set; }
        }

        // 업로드용 DTO (CSV 헤더와 매핑)
        public class EmployeeUploadRow
        {
            public string? Name { get; set; }
            public string? Department { get; set; }
            public string? JoinDate { get; set; }
        }

        // 업로드용 매핑 (영문/한글 헤더 모두 지원)
        public sealed class EmployeeUploadRowMap : ClassMap<EmployeeUploadRow>
        {
            public EmployeeUploadRowMap()
            {
                Map(m => m.Name).Name("Name", "이름");
                Map(m => m.Department).Name("Department", "부서");
                Map(m => m.JoinDate).Name("JoinDate", "입사일");
            }
        }

        public class ImportResult
        {
            public int RowNumber { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        // =============================
        // ========= In-Memory =========
        // =============================

        // 데모용 직원 in-memory 데이터
        private readonly List<Employee> _employees =
        [
            new() { Id = 1, Name = "홍길동", Department = "영업", JoinDate = new DateTime(2021, 1, 10) },
            new() { Id = 2, Name = "김철수", Department = "인사", JoinDate = new DateTime(2022, 3, 1) },
            new() { Id = 3, Name = "이영희", Department = "개발", JoinDate = new DateTime(2020, 7, 15) },
            new() { Id = 4, Name = "박민수", Department = "개발", JoinDate = new DateTime(2019, 12, 2) },
            new() { Id = 5, Name = "최유리", Department = "총무", JoinDate = new DateTime(2023, 4, 18) }
        ];

        // =============================
        // ========= UI State ==========
        // =============================

        private bool _isFileSelected;
        private bool _isUploading;
        private IBrowserFile? _uploadedFile;

        private int _successCount;
        private int _failureCount;

        private List<ImportResult> _importResults = new();

        // =============================
        // ====== Public Actions =======
        // =============================

        // 업로드용 템플릿 다운로드 (CSV)
        private async Task DownloadTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,Department,JoinDate");
            sb.AppendLine("예: 홍길동,영업,2024-01-01");

            await DownloadCsvAsync(TemplateFileName, sb.ToString());
        }

        // 현재 데이터 전체 다운로드 (CSV)
        private async Task DownloadCurrentData()
        {
            using var sw = new StringWriter();

            using (var csv = new CsvWriter(sw, CultureInfo.InvariantCulture))
            {
                csv.WriteField("Id");
                csv.WriteField("Name");
                csv.WriteField("Department");
                csv.WriteField("JoinDate");
                csv.NextRecord();

                foreach (var e in _employees)
                {
                    csv.WriteField(e.Id);
                    csv.WriteField(e.Name);
                    csv.WriteField(e.Department);
                    csv.WriteField(e.JoinDate.ToString("yyyy-MM-dd"));
                    csv.NextRecord();
                }
            }

            await DownloadCsvAsync(ExportFileName, sw.ToString());
        }

        // 파일 선택
        private void HandleFileSelected(InputFileChangeEventArgs e)
        {
            _uploadedFile   = e.File;
            _isFileSelected = _uploadedFile is not null;

            // 새 파일 선택 시 이전 결과 상태는 초기화
            ResetImportState();
        }

        // 업로드 시작 (신규 데이터 추가 전용)
        private async Task StartImport()
        {
            if (!_isFileSelected || _isUploading || _uploadedFile is null)
                return;

            _isUploading = true;
            ResetImportState();

            try
            {
                var fileContent = await ReadFileContentAsync(_uploadedFile);
                await ProcessCsvAsync(fileContent);
            }
            catch (Exception ex)
            {
                AddFailure(0, $"업로드 중 오류 발생: {ex.Message}");
            }
            finally
            {
                _isUploading = false;
                await InvokeAsync(StateHasChanged);
                // ⚠️ 여기서 _activeStep 변경하지 않음 (자동 이동 X)
            }
        }

        // =============================
        // ======= CSV Processing ======
        // =============================

        private async Task<string> ReadFileContentAsync(IBrowserFile file)
        {
            await using var stream = file.OpenReadStream(maxAllowedSize: MaxUploadFileSizeBytes);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        private async Task ProcessCsvAsync(string fileContent)
        {
            using var stringReader = new StringReader(fileContent);

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord   = true,
                MissingFieldFound = null,
                BadDataFound      = null,
                TrimOptions       = TrimOptions.Trim
            };

            using var csv = new CsvReader(stringReader, config);
            csv.Context.RegisterClassMap<EmployeeUploadRowMap>();

            // 1행: 헤더 읽기
            if (!csv.Read())
            {
                AddFailure(1, "파일에 데이터가 없습니다.");
                return;
            }

            csv.ReadHeader();
            var rawHeaders = csv.HeaderRecord ?? Array.Empty<string>();

            // 헤더 유효성 검사 (정해진 필드만 허용)
            if (!ValidateHeader(rawHeaders))
            {
                // ValidateHeader에서 실패 메시지 넣었으므로 여기서 종료
                return;
            }

            var records    = csv.GetRecords<EmployeeUploadRow>();
            var lineNumber = 1; // 헤더가 1행

            foreach (var record in records)
            {
                lineNumber++;
                ProcessRow(lineNumber, record);
            }

            await Task.CompletedTask;
        }

        private void ProcessRow(int lineNumber, EmployeeUploadRow record)
        {
            var name        = record.Name?.Trim();
            var department  = record.Department?.Trim();
            var joinDateRaw = record.JoinDate?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                AddFailure(lineNumber, "이름(Name)이 비어 있습니다.");
                return;
            }

            if (!TryParseJoinDate(joinDateRaw, out var joinDate))
            {
                AddFailure(lineNumber,
                    $"입사일(JoinDate) 형식이 올바르지 않습니다. 값: {joinDateRaw}");
                return;
            }

            var newEmployee = new Employee
            {
                Id         = GetNextEmployeeId(),
                Name       = name,
                Department = department,
                JoinDate   = joinDate
            };

            _employees.Add(newEmployee);
            _successCount++;
        }

        private bool TryParseJoinDate(string? raw, out DateTime joinDate)
        {
            // 필요하면 여기서 포맷 추가 가능 (예: yyyy.MM.dd, yyyy/MM/dd 등)
            return DateTime.TryParse(raw, out joinDate);
        }

        // 헤더 검증: 오직 정해진 필드 조합만 허용
        private bool ValidateHeader(string[] rawHeaders)
        {
            // 공백 제거 + null 보호
            var headers = rawHeaders
                .Select(h => (h ?? string.Empty).Trim())
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .ToList();

            // 1) 열 개수: 정확히 3개만 허용
            if (headers.Count != RequiredHeaderCount)
            {
                AddFailure(1,
                    $"업로드 템플릿에는 {RequiredHeaderCount}개의 열만 있어야 합니다. " +
                    $"(현재 {headers.Count}개 열이 있습니다. 허용: Name,Department,JoinDate 또는 이름,부서,입사일)");
                return false;
            }

            // 2) Id 열이 포함되어 있으면 거부 (다운로드용 CSV를 잘못 올린 경우)
            if (headers.Any(h => string.Equals(h, "Id", StringComparison.OrdinalIgnoreCase)))
            {
                AddFailure(1,
                    "Id 열이 포함된 파일은 조회/백업용 파일입니다. " +
                    "업로드 템플릿에는 Name,Department,JoinDate 3개 열만 사용해주세요.");
                return false;
            }

            // 3) 허용되는 헤더 조합인지 확인 (영문 또는 한글)
            if (!IsEnglishHeader(headers) && !IsKoreanHeader(headers))
            {
                AddFailure(1,
                    "업로드 템플릿의 헤더가 올바르지 않습니다. " +
                    "허용되는 헤더: Name,Department,JoinDate 또는 이름,부서,입사일 (총 3개 열만)");
                return false;
            }

            return true;
        }

        private static bool IsEnglishHeader(List<string> headers)
        {
            var lower = headers
                .Select(h => h.ToLowerInvariant())
                .OrderBy(h => h)
                .ToArray();

            var englishAllowed = EnglishHeaderTemplate
                .Select(h => h.ToLowerInvariant())
                .OrderBy(h => h)
                .ToArray();

            return lower.SequenceEqual(englishAllowed);
        }

        private static bool IsKoreanHeader(List<string> headers)
        {
            var sortedHeaders = headers.OrderBy(h => h).ToArray();
            var sortedKorean  = KoreanHeaderTemplate.OrderBy(h => h).ToArray();

            return sortedHeaders.SequenceEqual(sortedKorean);
        }

        // =============================
        // ========= Utilities =========
        // =============================

        private int GetNextEmployeeId() =>
            _employees.Count == 0 ? 1 : _employees[^1].Id + 1;

        private async Task DownloadCsvAsync(string fileName, string content)
        {
            var bytes  = Encoding.UTF8.GetBytes(content);
            var base64 = Convert.ToBase64String(bytes);

            await JS.InvokeVoidAsync(
                "downloadFileFromBase64",
                fileName,
                CsvMimeType,
                base64
            );
        }

        private void ResetImportState()
        {
            _successCount  = 0;
            _failureCount  = 0;
            _importResults = new List<ImportResult>();
        }

        private void AddFailure(int rowNumber, string message)
        {
            _failureCount++;
            _importResults.Add(new ImportResult
            {
                RowNumber = rowNumber,
                Status    = ImportStatus.Failure,
                Message   = message
            });
        }
    }
}
