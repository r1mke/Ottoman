namespace Ottoman.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public record PageQuery(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    /// <summary>No list comes near this; a larger page number just reads past the end (an empty page).</summary>
    public const int MaxPage = 1_000_000;

    public int SafePage => Math.Clamp(Page, 1, MaxPage);
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    /// <summary>Multiplied in 64 bits and capped, so page=2147483647 cannot overflow into a negative OFFSET.</summary>
    public int Skip => (int)Math.Min((long)(SafePage - 1) * SafePageSize, int.MaxValue);
}
