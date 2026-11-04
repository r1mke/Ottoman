using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Accounts;
using Ottoman.Application.Auth;
using Ottoman.Application.Clients;
using Ottoman.Application.Common;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Identity;

public class ClientQueries(OttomanDbContext db, IAccessGate gate, Images.ImageUrlBuilder images, TimeProvider clock) : IClientQueries
{
    public async Task<PagedResult<ClientListItem>> ListAsync(string? search, PageQuery page, CancellationToken ct)
    {
        var clientRoleId = await db.Roles.Where(r => r.Name == Roles.Client).Select(r => r.Id).SingleAsync(ct);

        var query = db.Users.AsNoTracking()
            .Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == clientRoleId));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u =>
                (u.FirstName + " " + u.LastName).Contains(s) ||
                u.Email!.Contains(s) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(u => new
            {
                Item = new ClientListItem(u.Id, (u.FirstName + " " + u.LastName).Trim(), u.Email!, u.PhoneNumber, u.RegisteredAtUtc, u.IsBlocked, null),
                u.PhotoKey,
                u.PhotoZoom,
                u.LockoutEnabled,
                u.LockoutEnd,
            })
            .ToListAsync(ct);

        var now = clock.GetUtcNow().UtcDateTime;
        return new PagedResult<ClientListItem>(
            items.Select(x => x.Item with
            {
                PhotoUrl = images.AvatarUrl(x.PhotoKey, x.PhotoZoom),
                PhotoZoom = x.PhotoZoom,
                LockedUntilUtc = AccountLock.LockedUntil(x.LockoutEnabled, x.LockoutEnd, now),
            }).ToList(), total);
    }

    public async Task<Result<ClientDetail>> GetAsync(string id, CancellationToken ct)
    {
        if (!await IsClientAsync(id, ct)) return Result<ClientDetail>.NotFound();

        var u = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.FirstName, x.LastName, x.Email, x.PhoneNumber, x.RegisteredAtUtc, x.IsBlocked, x.PhotoKey, x.PhotoZoom, x.LockoutEnabled, x.LockoutEnd })
            .FirstAsync(ct);

        var now = clock.GetUtcNow().UtcDateTime;
        var reservations = db.Reservations.AsNoTracking().Where(r => r.ClientId == id);
        var completed = reservations.Where(r => r.Status == ReservationStatus.Completed);
        var fulfilled = db.Orders.AsNoTracking().Where(o => o.ClientId == id && o.Status == OrderStatus.Fulfilled);

        var stats = new ClientStats(
            Visits: await completed.CountAsync(ct),
            NoShows: await reservations.CountAsync(r => r.Status == ReservationStatus.NoShow, ct),
            Cancelled: await reservations.CountAsync(r => r.Status == ReservationStatus.Cancelled, ct),
            Upcoming: await reservations.CountAsync(
                r => r.StartUtc >= now && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed), ct),
            ServicesSpent: await completed.SumAsync(r => (decimal?)r.TotalPrice, ct) ?? 0m,
            Orders: await fulfilled.CountAsync(ct),
            ProductsSpent: await fulfilled.SumAsync(o => (decimal?)o.TotalPrice, ct) ?? 0m,
            LastVisitUtc: await completed.MaxAsync(r => (DateTime?)r.StartUtc, ct));

        var recent = await reservations.OrderByDescending(r => r.StartUtc).ThenByDescending(r => r.Id).Take(10)
            .Select(r => new
            {
                r.Id,
                r.StartUtc,
                r.Status,
                r.EmployeeId,
                r.TotalPrice,
                Services = r.Items.Select(i => i.ServiceName).ToList(),
            }).ToListAsync(ct);
        var employeeIds = recent.Select(r => r.EmployeeId).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(x => employeeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FirstName, x.LastName }).ToDictionaryAsync(x => x.Id, x => $"{x.FirstName} {x.LastName}".Trim(), ct);

        var orders = await db.Orders.AsNoTracking().Where(o => o.ClientId == id)
            .OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id).Take(10)
            .Select(o => new
            {
                o.Id,
                o.CreatedAtUtc,
                o.Status,
                o.TotalPrice,
                Items = o.Items.Select(i => new { i.Quantity, i.ProductName }).ToList(),
            }).ToListAsync(ct);

        return Result<ClientDetail>.Ok(new ClientDetail(
            u.Id, $"{u.FirstName} {u.LastName}".Trim(), u.Email!, u.PhoneNumber, u.RegisteredAtUtc, u.IsBlocked, stats,
            recent.Select(r => new ClientReservationRow(
                r.Id, r.StartUtc, r.Status.ToString(), names.GetValueOrDefault(r.EmployeeId, "-"),
                string.Join(", ", r.Services), r.TotalPrice)).ToList(),
            orders.Select(o => new ClientOrderRow(
                o.Id, o.CreatedAtUtc, o.Status.ToString(), o.TotalPrice,
                string.Join(", ", o.Items.Select(i => $"{i.Quantity}x {i.ProductName}")))).ToList(),
            images.AvatarUrl(u.PhotoKey, u.PhotoZoom), u.PhotoZoom,
            AccountLock.LockedUntil(u.LockoutEnabled, u.LockoutEnd, now)));
    }

    public async Task<Result<ClientListItem>> SetBlockedAsync(string id, bool blocked, CancellationToken ct)
    {
        if (!await IsClientAsync(id, ct)) return Result<ClientListItem>.NotFound();

        // One statement each, so a concurrent login cannot slip a fresh session in between.
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsBlocked, blocked), ct);
        if (blocked)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedUtc, now), ct);
        }
        gate.Invalidate(id);

        var row = await db.Users.AsNoTracking().Where(x => x.Id == id).Select(x => new
        {
            Item = new ClientListItem(x.Id, (x.FirstName + " " + x.LastName).Trim(), x.Email!, x.PhoneNumber, x.RegisteredAtUtc, x.IsBlocked, null),
            x.PhotoKey,
            x.PhotoZoom,
            x.LockoutEnabled,
            x.LockoutEnd,
        }).FirstAsync(ct);
        return Result<ClientListItem>.Ok(row.Item with
        {
            PhotoUrl = images.AvatarUrl(row.PhotoKey, row.PhotoZoom),
            PhotoZoom = row.PhotoZoom,
            LockedUntilUtc = AccountLock.LockedUntil(row.LockoutEnabled, row.LockoutEnd, clock.GetUtcNow().UtcDateTime),
        });
    }

    private async Task<bool> IsClientAsync(string id, CancellationToken ct) =>
        await (from ur in db.UserRoles
               join role in db.Roles on ur.RoleId equals role.Id
               where ur.UserId == id && role.Name == Roles.Client
               select ur.UserId).AnyAsync(ct);
}
