using Ottoman.Application.Common;

namespace Ottoman.Application.Clients;

public record ClientListItem(
    string Id,
    string FullName,
    string Email,
    string? Phone,
    DateTime RegisteredAtUtc,
    bool IsBlocked);

public interface IClientQueries
{
    Task<PagedResult<ClientListItem>> ListAsync(string? search, PageQuery page, CancellationToken ct);
}
