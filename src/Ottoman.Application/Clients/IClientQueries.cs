using Ottoman.Application.Common;

namespace Ottoman.Application.Clients;

public record ClientListItem(
    string Id,
    string FullName,
    string Email,
    string? Phone,
    DateTime RegisteredAtUtc,
    bool IsBlocked);

/// <summary>What the salon has done with this client, counted from reservations and orders (not self-reported).</summary>
public record ClientStats(
    int Visits,
    int NoShows,
    int Cancelled,
    int Upcoming,
    decimal ServicesSpent,
    int Orders,
    decimal ProductsSpent,
    DateTime? LastVisitUtc);

public record ClientReservationRow(int Id, DateTime StartUtc, string Status, string EmployeeName, string Services, decimal Total);

public record ClientOrderRow(int Id, DateTime CreatedAtUtc, string Status, decimal Total, string Items);

public record ClientDetail(
    string Id,
    string FullName,
    string Email,
    string? Phone,
    DateTime RegisteredAtUtc,
    bool IsBlocked,
    ClientStats Stats,
    IReadOnlyList<ClientReservationRow> Reservations,
    IReadOnlyList<ClientOrderRow> Orders);

public interface IClientQueries
{
    Task<PagedResult<ClientListItem>> ListAsync(string? search, PageQuery page, CancellationToken ct);

    Task<Result<ClientDetail>> GetAsync(string id, CancellationToken ct);

    /// <summary>
    /// Blocks or unblocks a client. Blocking ends every session at once: refresh tokens are revoked and the
    /// access token stops working on the next request. Only accounts with the Client role can be blocked.
    /// </summary>
    Task<Result<ClientListItem>> SetBlockedAsync(string id, bool blocked, CancellationToken ct);
}
