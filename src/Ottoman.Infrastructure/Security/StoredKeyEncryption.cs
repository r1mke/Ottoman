using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Security;

/// <summary>
/// Turning key encryption on only affects keys created afterwards. This replaces keys that were stored in plain
/// text before the secret was set: all keys are revoked, the next use creates a fresh (encrypted) one, and the plain
/// rows are deleted. Anything protected with the old keys (password-reset codes, valid for a day) stops working,
/// which is the safe direction. Does nothing when no secret is configured or no plain key is stored.
/// </summary>
public sealed class StoredKeyEncryption(
    OttomanDbContext db,
    IKeyManager keys,
    IDataProtectionProvider protection,
    IOptions<KeyEncryptionOptions> options,
    TimeProvider clock,
    ILogger<StoredKeyEncryption> log)
{
    public const string PlainMarker = "<masterKey";

    public async Task<int> EnsureEncryptedAsync(CancellationToken ct)
    {
        if (!options.Value.Configured) return 0;

        var hasPlain = await db.DataProtectionKeys.AnyAsync(k => k.Xml!.Contains(PlainMarker), ct);
        if (!hasPlain) return 0;

        keys.RevokeAllKeys(clock.GetUtcNow(), "Replaced because the stored keys were not encrypted.");

        // Using the protector makes the framework create a new default key, now encrypted by our encryptor.
        protection.CreateProtector("Ottoman.KeyEncryptionMigration").Protect("x");

        var removed = await db.DataProtectionKeys.Where(k => k.Xml!.Contains(PlainMarker)).ExecuteDeleteAsync(ct);
        log.LogWarning("Data protection keys were stored in plain text; replaced them with encrypted ones ({Count} removed)", removed);
        return removed;
    }
}
