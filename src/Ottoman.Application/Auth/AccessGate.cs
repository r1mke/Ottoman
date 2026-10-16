namespace Ottoman.Application.Auth;

/// <summary>
/// Asked on every authenticated request whether the sign-in behind a valid token may still use the API: the account
/// must exist and not be blocked, and the session (one sign-in) must not have been ended by a sign-out or a password change.
/// This is what makes blocking and signing out take effect immediately instead of when the 15-minute token expires.
/// </summary>
public interface IAccessGate
{
    Task<bool> IsAllowedAsync(string userId, Guid sessionId, CancellationToken ct);

    /// <summary>Forget what is known about the user (and their sessions) so the next request asks the database again.</summary>
    void Invalidate(string userId);
}
