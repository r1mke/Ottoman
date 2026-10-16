using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ottoman.Application.Push;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Push;

public class DeviceRegistry(OttomanDbContext db, TimeProvider clock, IOptions<PushOptions> options) : IDeviceRegistry
{
    public async Task RegisterAsync(string userId, RegisterDeviceRequest request, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var token = request.Token.Trim();

        // A phone moves to whoever signed in last: one atomic statement, no read-then-write.
        var rows = await db.DeviceTokens.Where(d => d.Token == token)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.UserId, userId).SetProperty(d => d.LastSeenUtc, now), ct);

        if (rows == 0)
        {
            try
            {
                db.DeviceTokens.Add(new DeviceToken
                {
                    UserId = userId,
                    Token = token,
                    Platform = request.Platform,
                    CreatedAtUtc = now,
                    LastSeenUtc = now,
                });
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // The same token was registered concurrently; the winner's row is as good as ours.
                db.ChangeTracker.Clear();
                await db.DeviceTokens.Where(d => d.Token == token)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.UserId, userId).SetProperty(d => d.LastSeenUtc, now), ct);
            }
        }

        await TrimAsync(userId, ct);
    }

    public Task UnregisterAsync(string userId, string token, CancellationToken ct) =>
        db.DeviceTokens.Where(d => d.UserId == userId && d.Token == token).ExecuteDeleteAsync(ct);

    /// <summary>Keeps the newest few phones per account so a runaway client cannot grow the table.</summary>
    private async Task TrimAsync(string userId, CancellationToken ct)
    {
        var keep = options.Value.MaxDevicesPerUser;
        var stale = await db.DeviceTokens.Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenUtc).ThenByDescending(d => d.Id)
            .Skip(keep).Select(d => d.Id).ToListAsync(ct);
        if (stale.Count > 0) await db.DeviceTokens.Where(d => stale.Contains(d.Id)).ExecuteDeleteAsync(ct);
    }
}
