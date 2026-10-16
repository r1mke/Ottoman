using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Idempotency;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Idempotency;

/// <summary>
/// The unique index on (user, key) is the referee: of two simultaneous requests with the same key exactly one
/// inserts the placeholder and runs; the other sees a conflict and is told it is in progress or gets the answer.
/// </summary>
public sealed class IdempotencyStore(IServiceScopeFactory scopes, TimeProvider clock) : IIdempotencyStore
{
    public async Task<BeginResult> BeginAsync(string userId, string key, string route, string requestHash, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var now = clock.GetUtcNow().UtcDateTime;

            try
            {
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    UserId = userId,
                    Key = key,
                    Route = route,
                    RequestHash = requestHash,
                    CreatedAtUtc = now,
                });
                await db.SaveChangesAsync(ct);
                return new BeginResult(BeginOutcome.Proceed);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                db.ChangeTracker.Clear();
            }

            var existing = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, ct);
            if (existing is null) continue; // it was abandoned between our insert and our read: try once more

            if (existing.Route != route || existing.RequestHash != requestHash) return new BeginResult(BeginOutcome.Mismatch);
            if (existing.StatusCode is { } status)
                return new BeginResult(BeginOutcome.Replay, status, existing.ResponseBody, existing.ContentType);

            if (now - existing.CreatedAtUtc < IdempotencyRules.InProgressTimeout) return new BeginResult(BeginOutcome.InProgress);

            // The first attempt never finished (process died): free the key and take it.
            await db.IdempotencyRecords.Where(r => r.Id == existing.Id && r.StatusCode == null).ExecuteDeleteAsync(ct);
        }
        return new BeginResult(BeginOutcome.InProgress);
    }

    public async Task CompleteAsync(string userId, string key, int statusCode, string body, string contentType, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        await db.IdempotencyRecords.Where(r => r.UserId == userId && r.Key == key)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.StatusCode, statusCode)
                .SetProperty(r => r.ResponseBody, body)
                .SetProperty(r => r.ContentType, contentType)
                .SetProperty(r => r.CompletedAtUtc, now), CancellationToken.None);
    }

    public async Task AbandonAsync(string userId, string key, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        await db.IdempotencyRecords.Where(r => r.UserId == userId && r.Key == key && r.StatusCode == null)
            .ExecuteDeleteAsync(CancellationToken.None);
    }
}
