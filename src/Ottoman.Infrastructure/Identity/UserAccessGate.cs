using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Ottoman.Application.Auth;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Identity;

/// <summary>
/// Remembers for a minute whether a sign-in is allowed, so the check does not cost a query per request.
/// Blocking, signing out and changing the password drop everything remembered about that user at once (one change token
/// per user); the short lifetime bounds how long another instance could keep a stale answer.
/// </summary>
public sealed class UserAccessGate(IMemoryCache cache, IServiceScopeFactory scopes, TimeProvider clock) : IAccessGate
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, CancellationTokenSource> _changes = new();

    public async Task<bool> IsAllowedAsync(string userId, Guid sessionId, CancellationToken ct)
    {
        var key = $"access:{userId}:{sessionId:N}";
        if (cache.TryGetValue(key, out bool known)) return known;

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var now = clock.GetUtcNow().UtcDateTime;
        var allowed = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && !u.IsBlocked, ct)
                      && await db.RefreshTokens.AsNoTracking().AnyAsync(
                          t => t.UserId == userId && t.SessionId == sessionId && t.RevokedUtc == null && t.ExpiresUtc > now, ct);

        using var entry = cache.CreateEntry(key);
        entry.Value = allowed;
        entry.AbsoluteExpirationRelativeToNow = Lifetime;
        entry.AddExpirationToken(new CancellationChangeToken(ChangeSource(userId).Token));
        return allowed;
    }

    public void Invalidate(string userId)
    {
        if (_changes.TryRemove(userId, out var source)) source.Cancel();
    }

    private CancellationTokenSource ChangeSource(string userId) => _changes.GetOrAdd(userId, _ => new CancellationTokenSource());
}
