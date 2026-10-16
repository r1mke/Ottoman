using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Ottoman.Application.Notifications;

namespace Ottoman.Infrastructure.Notifications;

public sealed class ResetTokenVault(IDataProtectionProvider provider) : IResetTokenVault
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(1);

    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector("Ottoman.Outbox.PasswordReset").ToTimeLimitedDataProtector();

    public string Seal(string token) => _protector.Protect(token, Lifetime);

    public string? Open(string sealedToken)
    {
        try
        {
            return _protector.Unprotect(sealedToken);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
