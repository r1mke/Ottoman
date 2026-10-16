using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Auth;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Identity;

/// <summary>
/// Remembers for a minute whether a user is allowed in, so the check does not cost a query per request.
/// Blocking and unblocking invalidate the entry in this process; the short lifetime bounds how long
/// any other instance could keep a stale answer.
/// </summary>
public sealed class UserAccessGate(IMemoryCache cache, IServiceScopeFactory scopes) : IAccessGate
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private static string Key(string userId) => $"access:{userId}";

    public async Task<bool> IsAllowedAsync(string userId, CancellationToken ct)
    {
        if (cache.TryGetValue(Key(userId), out bool known)) return known;

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var allowed = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && !u.IsBlocked, ct);
        cache.Set(Key(userId), allowed, Lifetime);
        return allowed;
    }

    public void Invalidate(string userId) => cache.Remove(Key(userId));
}
