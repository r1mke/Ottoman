using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Clients;
using Ottoman.Application.Common;
using Ottoman.Domain;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Identity;

public class ClientQueries(OttomanDbContext db) : IClientQueries
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
            .Select(u => new ClientListItem(
                u.Id, (u.FirstName + " " + u.LastName).Trim(), u.Email!, u.PhoneNumber, u.RegisteredAtUtc, u.IsBlocked))
            .ToListAsync(ct);

        return new PagedResult<ClientListItem>(items, total);
    }
}
