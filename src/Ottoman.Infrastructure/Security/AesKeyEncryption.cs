using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ottoman.Infrastructure.Security;

public sealed class KeyEncryptionOptions
{
    public const string Section = "DataProtection";

    /// <summary>Base64 of exactly 32 random bytes. Generate with: openssl rand -base64 32</summary>
    public string KeyEncryptionSecret { get; set; } = "";

    public bool Configured => KeyEncryptionSecret != "";

    public byte[] ParsedKey()
    {
        try
        {
            var key = Convert.FromBase64String(KeyEncryptionSecret);
            if (key.Length == 32) return key;
        }
        catch (FormatException)
        {
            // falls through to the same message
        }
        throw new InvalidOperationException("DataProtection:KeyEncryptionSecret must be base64 of exactly 32 bytes (openssl rand -base64 32).");
    }
}

/// <summary>
/// Encrypts the Data Protection master keys before they are stored in the database (AES-256-GCM), so a copy of
/// the database alone no longer lets anyone forge reset tokens. The secret lives in the environment, not in the database.
/// Keys written before the secret was set stay readable and are replaced when they rotate.
/// </summary>
public sealed class AesXmlEncryptor(byte[] key) : IXmlEncryptor
{
    public static readonly XNamespace Ns = "https://ottoman.ba/dp/aes-gcm";

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cipher = new byte[plaintext.Length];
        using (var aes = new AesGcm(key, tag.Length)) aes.Encrypt(nonce, plaintext, cipher, tag);

        var element = new XElement(Ns + "encryptedKey",
            new XElement(Ns + "value", Convert.ToBase64String([.. nonce, .. tag, .. cipher])));
        return new EncryptedXmlInfo(element, typeof(AesXmlDecryptor));
    }
}

public sealed class AesXmlDecryptor(IServiceProvider services) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        var key = services.GetRequiredService<IOptions<KeyEncryptionOptions>>().Value.ParsedKey();
        var blob = Convert.FromBase64String((string)encryptedElement.Element(AesXmlEncryptor.Ns + "value")!);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;
        var nonce = blob.AsSpan(0, nonceSize);
        var tag = blob.AsSpan(nonceSize, tagSize);
        var cipher = blob.AsSpan(nonceSize + tagSize);
        var plaintext = new byte[cipher.Length];
        using (var aes = new AesGcm(key, tagSize)) aes.Decrypt(nonce, cipher, tag, plaintext);
        return XElement.Parse(Encoding.UTF8.GetString(plaintext));
    }
}

public static class DataProtectionExtensions
{
    /// <summary>Turns encryption of the stored keys on when a secret is configured; without one the keys stay as they were.</summary>
    public static IDataProtectionBuilder ProtectKeysWithConfiguredSecret(this IDataProtectionBuilder builder, KeyEncryptionOptions options)
    {
        builder.Services.Configure<KeyEncryptionOptions>(o => o.KeyEncryptionSecret = options.KeyEncryptionSecret);
        if (!options.Configured) return builder;

        var key = options.ParsedKey();
        builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(
            new ConfigureOptions<KeyManagementOptions>(o => o.XmlEncryptor = new AesXmlEncryptor(key)));
        return builder;
    }
}
