using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ottoman.Application.Audit;
using Ottoman.Application.Common;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Audit;

public sealed class AuditLog(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AuditLog> log) : IAuditLog
{
    public async Task RecordAsync(AuditRecord record, CancellationToken ct)
    {
        try
        {
            // Its own scope: the request's context may be mid-transaction or already disposed of its state.
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();

            var name = await db.Users.AsNoTracking().Where(u => u.Id == record.UserId)
                .Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync(CancellationToken.None);

            db.AuditEntries.Add(new AuditEntry
            {
                AtUtc = clock.GetUtcNow().UtcDateTime,
                UserId = record.UserId,
                UserName = name ?? "(obrisan nalog)",
                Role = record.Role,
                Method = record.Method,
                Action = record.Action,
                Route = Truncate(record.Route, 200),
                Target = Truncate(record.Target, 200),
                StatusCode = record.StatusCode,
                Ip = record.Ip,
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Could not write the audit entry for {Action} by {UserId}", record.Action, record.UserId);
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}

public class AuditQueries(OttomanDbContext db) : IAuditQueries
{
    public async Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery q, PageQuery page, CancellationToken ct)
    {
        var query = db.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.UserId)) query = query.Where(e => e.UserId == q.UserId);
        if (q.FromUtc is { } from) query = query.Where(e => e.AtUtc >= from);
        if (q.ToUtc is { } to) query = query.Where(e => e.AtUtc < to);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(e => e.Action.Contains(s) || e.UserName.Contains(s) || e.Target.Contains(s));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(e => e.AtUtc).ThenByDescending(e => e.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(e => new AuditEntryDto(
                e.Id, e.AtUtc, e.UserId, e.UserName, e.Role, e.Method, e.Action, e.Route, e.Target, e.StatusCode, e.Ip))
            .ToListAsync(ct);
        return new PagedResult<AuditEntryDto>(items, total);
    }
}
