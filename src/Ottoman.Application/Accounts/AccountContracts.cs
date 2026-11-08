using FluentValidation;
using Ottoman.Application.Auth;
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

/// <summary>
/// The admin picks a new password for a client or barber. The old password is never asked for (it is not the admin's);
/// the app asks twice, but only the new value travels.
/// </summary>
public record AdminSetPasswordRequest(string NewPassword);

public class AdminSetPasswordRequestValidator : AbstractValidator<AdminSetPasswordRequest>
{
    public AdminSetPasswordRequestValidator() => RuleFor(x => x.NewPassword).Strong();
}

/// <summary>Admin actions on any client's or barber's account. Admin-only at the HTTP edge.</summary>
public interface IAccountAdmin
{
    /// <summary>
    /// Ends a sign-in lock at once and forgets the wrong-password count. NotFound for unknown ids and for accounts
    /// that are neither a client nor a barber (the admin's own account is not unlocked from here).
    /// </summary>
    Task<Result<AccountLockDto>> UnlockAsync(string id, CancellationToken ct);

    /// <summary>
    /// Sets a new password for a client or barber without the old one. Ends every session of that account and forgets
    /// its push devices (as any password change does), and ends a sign-in lock. NotFound for unknown ids and for
    /// accounts that are neither a client nor a barber; Invalid when the password breaks the strength policy.
    /// </summary>
    Task<Result<bool>> SetPasswordAsync(string id, string newPassword, CancellationToken ct);
}
