namespace Loja.SharedKernel;

/// <summary>
/// One page of a larger list.
/// </summary>
/// <param name="Page">1-based page number.</param>
/// <param name="TotalCount">Number of items across all pages.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page < TotalPages;
}
