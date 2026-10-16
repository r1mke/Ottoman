using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Audit;
using Ottoman.Application.Idempotency;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Outbox;

/// <summary>
/// Keeps the outbox from growing forever, and keeps secrets from lingering in it: a password-reset event carries
/// the reset token, so those rows go after a day; everything else that was delivered goes after a week.
/// Rows that were never delivered are never touched: they are evidence of a problem, not garbage.
/// </summary>
public class OutboxCleanup(OttomanDbContext db, TimeProvider clock)
{
    public static readonly TimeSpan KeepDelivered = TimeSpan.FromDays(7);
    public static readonly TimeSpan KeepSecrets = TimeSpan.FromDays(1);
    public static readonly TimeSpan KeepProcessedMarkers = TimeSpan.FromDays(30);

    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var delivered = now - KeepDelivered;
        var secrets = now - KeepSecrets;
        var markers = now - KeepProcessedMarkers;

        var removed = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc != null &&
                        (m.ProcessedAtUtc < delivered || (m.Type == "PasswordResetRequested" && m.ProcessedAtUtc < secrets)))
            .ExecuteDeleteAsync(ct);

        // The duplicate-delivery markers only matter while a redelivery is still possible.
        removed += await db.ProcessedEvents.Where(p => p.ProcessedAtUtc < markers).ExecuteDeleteAsync(ct);

        // The audit trail is kept for its retention period and not a day longer (it holds names and IP addresses).
        var auditCutoff = now - AuditPolicy.Retention;
        removed += await db.AuditEntries.Where(a => a.AtUtc < auditCutoff).ExecuteDeleteAsync(ct);

        var idempotencyCutoff = now - IdempotencyRules.Retention;
        removed += await db.IdempotencyRecords.Where(r => r.CreatedAtUtc < idempotencyCutoff).ExecuteDeleteAsync(ct);
        return removed;
    }
}
