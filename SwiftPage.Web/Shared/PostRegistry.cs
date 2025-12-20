public enum PostCategory
{
    GettingStarted,
    Components,
    Api
}

public class PostPage
{
    public string Route { get; init; } = default!;
    public string Title { get; init; } = default!;
    public string? Description { get; init; }
    public PostCategory Category { get; init; }
    public int Order { get; init; }
}

public static class PostRegistry
{
    public static readonly PostPage[] Pages =
    [
        new PostPage
    {
        Route = "/docs",
        Title = "개요",
        Description = "BuildVision 템플릿의 목적과 문서 전체 구성에 대한 간단한 소개",
        Category = PostCategory.GettingStarted,
        Order = 1
    },
    new PostPage
    {
        Route = "/docs/components/data-grid",
        Title = "데이터 그리드",
        Description = "조회·정렬·검색·편집 등 표 기반 업무 화면을 구성하는 핵심 요소를 설명합니다.",
        Category = PostCategory.Components,
        Order = 1
    },
    new PostPage
    {
        Route = "/docs/components/data-upload",
        Title = "데이터 업로드",
        Description = "엑셀·CSV 파일을 활용한 대량 데이터 가져오기 기능의 사용 방식과 처리 흐름을 안내합니다.",
        Category = PostCategory.Components,
        Order = 2
    },
    new PostPage
    {
        Route = "/docs/components/role-visibility",
        Title = "권한 기반 표시",
        Description = "로그인한 사용자의 역할에 따라 UI 요소를 선택적으로 표시하거나 숨기는 방법을 설명합니다.",
        Category = PostCategory.Components,
        Order = 3
    },
    new PostPage
    {
        Route = "/docs/api",
        Title = "API 개요",
        Description = "시스템 연동을 위한 API 구조, 호출 방식, 인증 방식을 정리한 안내 페이지입니다.",
        Category = PostCategory.Api,
        Order = 1
    }
    ];
}
