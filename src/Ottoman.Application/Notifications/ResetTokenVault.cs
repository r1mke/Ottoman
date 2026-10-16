namespace Ottoman.Application.Notifications;

/// <summary>
/// Keeps the password-reset code unreadable while it waits in the outbox: the API seals it, only the worker that
/// mails it opens it. A sealed code expires on its own, so an old database copy holds nothing usable.
/// </summary>
public interface IResetTokenVault
{
    string Seal(string token);

    /// <summary>The original code, or null when the sealed value is damaged or has expired.</summary>
    string? Open(string sealedToken);
}
