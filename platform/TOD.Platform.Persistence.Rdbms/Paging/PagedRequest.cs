namespace TOD.Platform.Persistence.Rdbms.Paging;

public sealed class PagedRequest
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 200;

    public int PageNumber { get; set; } = DefaultPageNumber;

    public int PageSize { get; set; } = DefaultPageSize;

    public (int PageNumber, int PageSize) Normalize()
    {
        var pageNumber = PageNumber < 1 ? DefaultPageNumber : PageNumber;
        var pageSize = PageSize < 1 ? DefaultPageSize : PageSize;
        pageSize = pageSize > MaxPageSize ? MaxPageSize : pageSize;

        return (pageNumber, pageSize);
    }
}
