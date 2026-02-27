namespace TOD.Platform.Persistence.Rdbms.Paging;

public sealed class PagedResult<T>
{
    public PagedResult(IEnumerable<T> items, int pageNumber, int pageSize, int totalCount)
    {
        Items = items.ToList();
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}
