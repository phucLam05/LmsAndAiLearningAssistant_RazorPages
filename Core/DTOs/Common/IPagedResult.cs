namespace Core.DTOs.Common
{
    public interface IPagedResult
    {
        int TotalCount { get; }
        int PageIndex { get; }
        int PageSize { get; }
        int TotalPages { get; }
        bool HasPreviousPage { get; }
        bool HasNextPage { get; }
    }
}
