using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text.Json;

namespace SwiftPage.Web.Pages.Docs.Components
{
    public partial class DataGrid : ComponentBase, IAsyncDisposable
    {
        [Inject] private IJSRuntime JS { get; set; } = default!;
        // ============================
        // ========= Entities =========
        // ============================
        public class Employee
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Department { get; set; }
            public DateTime JoinDate { get; set; }

            public Employee Clone() => new()
            {
                Id = Id,
                Name = Name,
                Department = Department,
                JoinDate = JoinDate
            };
        }

        private readonly List<Employee> _employees =
        [
            new() { Id = 1, Name = "홍길동", Department = "영업",   JoinDate = new DateTime(2021, 1, 10) },
            new() { Id = 2, Name = "김철수", Department = "인사",   JoinDate = new DateTime(2022, 3, 1) },
            new() { Id = 3, Name = "이영희", Department = "개발",   JoinDate = new DateTime(2020, 7, 15) },
            new() { Id = 4, Name = "박민수", Department = "개발",   JoinDate = new DateTime(2019, 12, 2) },
            new() { Id = 5, Name = "최유리", Department = "총무",   JoinDate = new DateTime(2023, 4, 18) },
            new() { Id = 6, Name = "정우진", Department = "영업",   JoinDate = new DateTime(2021, 8, 30) },
            new() { Id = 7, Name = "한지민", Department = "인사",   JoinDate = new DateTime(2020, 11, 5) },
            new() { Id = 8, Name = "오세훈", Department = "개발",   JoinDate = new DateTime(2022, 6, 14) },
            new() { Id = 9, Name = "강수지", Department = "마케팅", JoinDate = new DateTime(2018, 9, 9) },
            new() { Id = 10, Name = "류현우", Department = "총무",  JoinDate = new DateTime(2023, 2, 22) },
        ];

        // 공통: 다음 ID 생성
        private int GetNextEmployeeId() =>
            _employees.Count == 0 ? 1 : _employees.Max(e => e.Id) + 1;


        // ==========================
        // ======== 공통 삭제 =======
        // ==========================

        private void Delete(int id)
        {
            var target = _employees.FirstOrDefault(e => e.Id == id);
            if (target is not null)
                _employees.Remove(target);
        }


        // ==========================
        // ======= 모달 편집 ========
        // ==========================

        private bool _isDialogOpen;
        private bool _isNew;
        private string _dialogTitle = string.Empty;
        private Employee _dialogModel = new();

        private void OpenCreateDialog()
        {
            _isNew = true;

            _dialogModel = new Employee
            {
                Id = GetNextEmployeeId(),
                Name = string.Empty,
                Department = string.Empty,
                JoinDate = DateTime.Today
            };

            _dialogTitle = "신규 직원 등록";
            _isDialogOpen = true;
            StateHasChanged();
        }

        private void OpenEditDialog(Employee employee)
        {
            _isNew = false;
            _dialogModel = employee.Clone();

            _dialogTitle = $"직원 정보 수정 (ID: {employee.Id})";
            _isDialogOpen = true;
        }

        private async Task SaveDialog()
        {
            if (_isNew)
            {
                _employees.Add(_dialogModel);
            }
            else
            {
                var index = _employees.FindIndex(e => e.Id == _dialogModel.Id);
                if (index >= 0)
                    _employees[index] = _dialogModel;
            }

            _isDialogOpen = false;
            StateHasChanged();
            await UpdateTabulators();
        }

        private void CloseDialog()
        {
            _isDialogOpen = false;
        }

        // 탭 인덱스 관리
        private int _activeTabIndex = 0;

        private async Task OnTabChanged(int index)
        {
            _activeTabIndex = index;
            StateHasChanged();
            
            // 탭이 완전히 전환될 때까지 대기
            await Task.Delay(200);

            if (index == 0) // 인라인 편집 탭
            {
                // 테이블이 이미 초기화되어 있으면 다시 초기화
                if (_tabulatorIds.Contains("inlineEditTable"))
                {
                    await JS.InvokeVoidAsync("tabulatorHelper.destroy", "inlineEditTable");
                    _tabulatorIds.Remove("inlineEditTable");
                }
                await Task.Delay(50);
                await InitInlineEditTable();
            }
            else if (index == 1) // 모달 편집 탭
            {
                // 테이블이 이미 초기화되어 있으면 다시 초기화
                if (_tabulatorIds.Contains("modalEditTable"))
                {
                    await JS.InvokeVoidAsync("tabulatorHelper.destroy", "modalEditTable");
                    _tabulatorIds.Remove("modalEditTable");
                }
                await Task.Delay(50);
                await InitModalEditTable();
            }
        }


        // ==============================
        // ===== 검색 / 필터링 공통 =====
        // ==============================

        // 부서 필터 옵션 (Tabulator headerFilter용)
        private IEnumerable<string?> DepartmentOptions =>
            _employees
                .Select(e => e.Department)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .OrderBy(d => d);

        // =============================
        // ====== Tabulator 관련 ======
        // =============================

        private readonly List<string> _tabulatorIds = new();

        private DotNetObjectReference<DataGrid>? _objRef;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                _objRef = DotNetObjectReference.Create(this);
                await JS.InvokeVoidAsync("setBlazorTabulatorRef", _objRef);
                // DOM이 완전히 렌더링될 때까지 약간의 지연
                await Task.Delay(100);
                await InitializeTabulators();
            }
            else
            {
                await UpdateTabulators();
            }
        }

        [JSInvokable]
        public Task HandleCellEdited(string tableId, object rowData)
        {
            try
            {
                var json = JsonSerializer.Serialize(rowData);
                var employeeData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                if (employeeData != null && employeeData.TryGetValue("id", out var idElement))
                {
                    var id = idElement.GetInt32();
                    var employee = _employees.FirstOrDefault(e => e.Id == id);

                    if (employee != null)
                    {
                        if (employeeData.TryGetValue("name", out var nameElement))
                            employee.Name = nameElement.GetString();
                        if (employeeData.TryGetValue("department", out var deptElement))
                            employee.Department = deptElement.GetString();
                        if (employeeData.TryGetValue("joinDate", out var dateElement))
                        {
                            var dateStr = dateElement.GetString();
                            if (DateTime.TryParse(dateStr, out var date))
                                employee.JoinDate = date;
                        }

                        StateHasChanged();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"셀 편집 처리 오류: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        [JSInvokable]
        public async Task HandleDelete(int id)
        {
            Delete(id);
            StateHasChanged();
            await UpdateTabulators();
        }

        [JSInvokable]
        public void HandleOpenEditDialog(int id)
        {
            var employee = _employees.FirstOrDefault(e => e.Id == id);
            if (employee != null)
            {
                OpenEditDialog(employee);
                StateHasChanged();
            }
        }

        private async Task InitializeTabulators()
        {
            try
            {
                // 필터링 테이블과 페이지네이션 테이블은 항상 보이므로 먼저 초기화
                await InitFilterTable();
                await InitPaginationTable();

                // 탭 내부 테이블은 지연 초기화 (탭이 활성화될 때)
                // 초기에는 첫 번째 탭만 초기화
                await Task.Delay(150); // DOM이 완전히 렌더링될 때까지 대기
                await InitInlineEditTable();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tabulator 초기화 오류: {ex.Message}");
            }
        }

        private async Task UpdateTabulators()
        {
            try
            {
                // 인라인 편집 테이블 업데이트
                if (_tabulatorIds.Contains("inlineEditTable"))
                {
                    var tableData = _employees.Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        department = e.Department,
                        joinDate = e.JoinDate.ToString("yyyy-MM-dd")
                    }).ToList();

                    await JS.InvokeVoidAsync("tabulatorHelper.setData", "inlineEditTable", tableData);
                }

                // 모달 편집 테이블 업데이트
                if (_tabulatorIds.Contains("modalEditTable"))
                {
                    var tableData = _employees.Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        department = e.Department,
                        joinDate = e.JoinDate.ToString("yyyy-MM-dd"),
                        actions = GetActionButtonsHtml(e.Id, false)
                    }).ToList();

                    await JS.InvokeVoidAsync("tabulatorHelper.setData", "modalEditTable", tableData);
                }

                // 필터링 테이블과 페이지네이션 테이블은 Tabulator의 headerFilter로 자동 필터링되므로
                // 데이터만 업데이트 (필터는 Tabulator가 자동 처리)
                if (_tabulatorIds.Contains("filterTable"))
                {
                    var tableData = _employees.Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        department = e.Department,
                        joinDate = e.JoinDate.ToString("yyyy-MM-dd")
                    }).ToList();

                    await JS.InvokeVoidAsync("tabulatorHelper.setData", "filterTable", tableData);
                }

                if (_tabulatorIds.Contains("paginationTable"))
                {
                    var tableData = _employees.Select(e => new
                    {
                        id = e.Id,
                        name = e.Name,
                        department = e.Department,
                        joinDate = e.JoinDate.ToString("yyyy-MM-dd")
                    }).ToList();

                    await JS.InvokeVoidAsync("tabulatorHelper.setData", "paginationTable", tableData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Tabulator 업데이트 오류: {ex.Message}");
            }
        }

        private async Task InitInlineEditTable()
        {
            var columns = new object[]
            {
                new {
                    title = "ID",
                    field = "id",
                    width = 80,
                    editor = false
                },
                new {
                    title = "이름",
                    field = "name",
                    editor = "input",
                    editable = true
                },
                new {
                    title = "부서",
                    field = "department",
                    editor = "input",
                    editable = true
                },
                new {
                    title = "입사일",
                    field = "joinDate",
                    editor = "input",
                    editable = true
                },
                new {
                    title = "삭제",
                    field = "actions",
                    formatter = "buttonCross",
                    width = 80,
                    hozAlign = "center"
                }
            };

            var data = _employees.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                department = e.Department,
                joinDate = e.JoinDate.ToString("yyyy-MM-dd")
            }).ToList();

            var options = new
            {
                onCellEdited = true,
                addRowPos = "top"
            };

            await JS.InvokeVoidAsync("tabulatorHelper.init", "inlineEditTable", columns, data, options);
            _tabulatorIds.Add("inlineEditTable");
        }

        private async Task InitModalEditTable()
        {
            var columns = new object[]
            {
                new { title = "ID", field = "id", width = 80 },
                new { title = "이름", field = "name" },
                new { title = "부서", field = "department" },
                new { title = "입사일", field = "joinDate" },
                new {
                    title = "작업",
                    field = "actions",
                    formatter = "html",
                    width = 150,
                    hozAlign = "center"
                }
            };

            var data = _employees.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                department = e.Department,
                joinDate = e.JoinDate.ToString("yyyy-MM-dd"),
                actions = GetActionButtonsHtml(e.Id, false)
            }).ToList();

            await JS.InvokeVoidAsync("tabulatorHelper.init", "modalEditTable", columns, data, new { });
            _tabulatorIds.Add("modalEditTable");
        }

        private async Task InitFilterTable()
        {
            var deptOptions = DepartmentOptions.ToList();
            
            var columns = new object[]
            {
                new { 
                    title = "ID", 
                    field = "id", 
                    width = 80,
                    headerFilter = "input",
                    headerFilterPlaceholder = "ID 검색"
                },
                new { 
                    title = "이름", 
                    field = "name",
                    headerFilter = "input",
                    headerFilterPlaceholder = "이름 검색"
                },
                new { 
                    title = "부서", 
                    field = "department",
                    // 부서 전용 커스텀 셀렉트 헤더 필터 (JS에서 departmentSelect 처리)
                    headerFilter = "departmentSelect",
                    headerFilterParams = new { 
                        values = deptOptions,
                        clearable = true
                    },
                    headerFilterPlaceholder = "부서 검색"
                },
                new { 
                    title = "입사일", 
                    field = "joinDate",
                    headerFilter = "input",
                    headerFilterPlaceholder = "입사일 검색"
                }
            };

            var data = _employees.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                department = e.Department,
                joinDate = e.JoinDate.ToString("yyyy-MM-dd")
            }).ToList();

            await JS.InvokeVoidAsync("tabulatorHelper.init", "filterTable", columns, data, new { });
            _tabulatorIds.Add("filterTable");
        }

        private async Task InitPaginationTable()
        {
            var deptOptions = DepartmentOptions.ToList();
            
            var columns = new object[]
            {
                new { 
                    title = "ID", 
                    field = "id", 
                    width = 80,
                    headerFilter = "input",
                    headerFilterPlaceholder = "ID 검색"
                },
                new { 
                    title = "이름", 
                    field = "name",
                    headerFilter = "input",
                    headerFilterPlaceholder = "이름 검색"
                },
                new { 
                    title = "부서", 
                    field = "department",
                    // 부서 전용 커스텀 셀렉트 헤더 필터 (JS에서 departmentSelect 처리)
                    headerFilter = "departmentSelect",
                    headerFilterParams = new { 
                        values = deptOptions,
                        clearable = true
                    }
                },
                new { 
                    title = "입사일", 
                    field = "joinDate",
                    headerFilter = "input",
                    headerFilterPlaceholder = "입사일 검색"
                }
            };

            var data = _employees.Select(e => new
            {
                id = e.Id,
                name = e.Name,
                department = e.Department,
                joinDate = e.JoinDate.ToString("yyyy-MM-dd")
            }).ToList();

            var options = new
            {
                pagination = true,
                paginationSize = 10,
                paginationSizeSelector = new[] { 5, 10, 20 }
            };

            await JS.InvokeVoidAsync("tabulatorHelper.init", "paginationTable", columns, data, options);
            _tabulatorIds.Add("paginationTable");
        }

        // 검색/필터링 조건 초기화 (filterTable 전체 필터 해제)
        private async Task ClearFilterConditions()
        {
            await JS.InvokeVoidAsync("tabulatorHelper.clearFilter", "filterTable");
        }

        private string GetActionButtonsHtml(int employeeId, bool isInline)
        {
            // 현재 데모에서는 모달 편집 액션만 사용
            return $@"
                <button class='tabulator-action-btn' 
                        data-action='openEdit' data-id='{employeeId}' 
                        style='padding: 4px 8px; margin-right: 4px; background: transparent; color: #1976d2; border: 1px solid #1976d2; border-radius: 4px; cursor: pointer; font-size: 12px;'>
                    보기/수정
                </button>
                <button class='tabulator-action-btn' 
                        data-action='delete' data-id='{employeeId}'
                        style='padding: 4px 8px; background: transparent; color: #d32f2f; border: none; cursor: pointer; font-size: 12px;'>
                    삭제
                </button>";
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var id in _tabulatorIds)
            {
                try
                {
                    await JS.InvokeVoidAsync("tabulatorHelper.destroy", id);
                }
                catch { }
            }
            _objRef?.Dispose();
        }
    }
}
