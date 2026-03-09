namespace MarcoERP.API.Controllers;

public class PaginatedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }

    public int Skip => (Page - 1) * PageSize;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 10;
        if (PageSize > 200) PageSize = 200;
    }
}

public class PaginatedResponse<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}
