namespace Ottoman.Application.Auth;

/// <summary>
/// Asked on every authenticated request whether the account behind a valid token may still use the API.
/// This is what makes blocking a user take effect immediately instead of when the 15-minute token expires.
/// </summary>
public interface IAccessGate
{
    Task<bool> IsAllowedAsync(string userId, CancellationToken ct);

    /// <summary>Forget what is known about the user so the next request asks the database again.</summary>
    void Invalidate(string userId);
}
