using Ottoman.Application.Common;

namespace Ottoman.Application.Accounts;

/// <summary>An account's sign-in lock after too many wrong passwords. Null = not locked.</summary>
public record AccountLockDto(string Id, DateTime? LockedUntilUtc);

/// <summary>Turns Identity's lockout fields into what the admin sees. Pure.</summary>
public static class AccountLock
{
    /// <summary>
    /// When the lock ends, or null when the account is not locked right now: lockout switched off for the account,
    /// no end set, or an end that has already passed.
    /// </summary>
    public static DateTime? LockedUntil(bool lockoutEnabled, DateTimeOffset? lockoutEnd, DateTime nowUtc) =>
        lockoutEnabled && lockoutEnd is { } end && end.UtcDateTime > nowUtc ? end.UtcDateTime : null;
}

/// <summary>Admin actions on any client's or barber's account. Admin-only at the HTTP edge.</summary>
public interface IAccountAdmin
{
    /// <summary>
    /// Ends a sign-in lock at once and forgets the wrong-password count. NotFound for unknown ids and for accounts
    /// that are neither a client nor a barber (the admin's own account is not unlocked from here).
    /// </summary>
    Task<Result<AccountLockDto>> UnlockAsync(string id, CancellationToken ct);
}
