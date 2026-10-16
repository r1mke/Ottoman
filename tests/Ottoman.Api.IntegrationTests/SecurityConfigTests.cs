using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Security;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class SecurityConfigTests(ApiFixture api)
{
    private const string Allowed = "https://admin.example.com";

    private static HttpRequestMessage Preflight(string origin, string method = "POST", string headers = "authorization,content-type,idempotency-key")
    {
        var r = new HttpRequestMessage(HttpMethod.Options, "/api/v1/orders");
        r.Headers.Add("Origin", origin);
        r.Headers.Add("Access-Control-Request-Method", method);
        r.Headers.Add("Access-Control-Request-Headers", headers);
        return r;
    }

    // ------------------------------------------------------------------ cors

    [Fact]
    public async Task A_listed_origin_passes_the_preflight_with_only_what_it_asked_for()
    {
        var res = await api.CreateClient().SendAsync(Preflight(Allowed));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        res.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle(Allowed);
        res.Headers.GetValues("Access-Control-Allow-Headers").Single().Should().Contain("Idempotency-Key", "or browsers could not send the key").And.Contain("Authorization");
        res.Headers.GetValues("Access-Control-Allow-Methods").Single().Should().Contain("POST");
        res.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse("tokens travel in a header, never as ambient cookies");
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("http://admin.example.com")]
    [InlineData("https://admin.example.com.evil.com")]
    [InlineData("null")]
    public async Task Any_other_origin_gets_no_permission(string origin)
    {
        var res = await api.CreateClient().SendAsync(Preflight(origin));

        res.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task A_header_that_was_not_agreed_is_not_allowed_even_from_a_listed_origin()
    {
        var res = await api.CreateClient().SendAsync(Preflight(Allowed, headers: "x-evil"));

        // The browser only proceeds if every requested header is echoed back as allowed; it must not be.
        var allowed = res.Headers.TryGetValues("Access-Control-Allow-Headers", out var values) ? string.Join(",", values) : "";
        allowed.ToLowerInvariant().Should().NotContain("x-evil");
    }

    [Fact]
    public async Task The_replay_marker_is_readable_from_the_browser_and_requests_without_origin_are_untouched()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("Origin", Allowed);
        var res = await api.CreateClient().SendAsync(request);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Headers.GetValues("Access-Control-Allow-Origin").Should().ContainSingle(Allowed);
        res.Headers.GetValues("Access-Control-Expose-Headers").Single().Should().Contain("Idempotent-Replayed");

        var plain = await api.CreateClient().GetAsync("/health/live");
        plain.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    // -------------------------------------------------- stored data protection keys

    [Fact]
    public async Task Data_protection_keys_in_the_database_are_encrypted_not_plain_xml()
    {
        // Resetting a password makes the app protect a token, which makes it create a key.
        var (email, _) = await api.CreateClient().NewClient();
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).EnsureSuccessStatusCode();

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var xml = await db.DataProtectionKeys.AsNoTracking().Select(k => k.Xml!).ToListAsync();

        xml.Should().Contain(x => x.Contains("https://ottoman.ba/dp/aes-gcm") && x.Contains("<value>"), "at least the newest key is encrypted");
        xml.Where(x => x.Contains("https://ottoman.ba/dp/aes-gcm")).Should().OnlyContain(x => !x.Contains("<masterKey"));
    }

    [Fact]
    public async Task Tokens_still_work_with_encrypted_keys_after_a_full_reset_flow()
    {
        var (email, _) = await api.CreateClient().NewClient();
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).EnsureSuccessStatusCode();

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var payload = (await db.OutboxMessages.AsNoTracking().Where(m => m.Type == "PasswordResetRequested").OrderByDescending(m => m.Id)
            .Select(m => m.PayloadJson).Take(50).ToListAsync()).First(p => p.Contains(email));
        var token = System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("Token").GetString()!;

        var reset = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token, newPassword = "N0va_Lozinka!" });

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "N0va_Lozinka!" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Plain_text_keys_from_before_the_secret_was_set_are_replaced_and_tokens_keep_working()
    {
        var plainId = Guid.NewGuid();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            db.DataProtectionKeys.Add(new Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey
            {
                FriendlyName = $"key-{plainId}",
                Xml = $"<key id=\"{plainId}\" version=\"1\"><creationDate>2020-01-01T00:00:00Z</creationDate><activationDate>2020-01-01T00:00:00Z</activationDate><expirationDate>2020-02-01T00:00:00Z</expirationDate><descriptor deserializerType=\"x\"><descriptor><masterKey>AAAA</masterKey></descriptor></descriptor></key>",
            });
            await db.SaveChangesAsync();
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var removed = await scope.ServiceProvider.GetRequiredService<StoredKeyEncryption>().EnsureEncryptedAsync(default);
            removed.Should().BeGreaterThanOrEqualTo(1);
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var xml = await db.DataProtectionKeys.AsNoTracking().Select(k => k.Xml!).ToListAsync();
            xml.Should().NotContain(x => x.Contains("<masterKey"), "no plain master key is left anywhere");
            xml.Should().Contain(x => x.Contains("https://ottoman.ba/dp/aes-gcm"), "a fresh encrypted key took over");
        }

        // The app still protects and unprotects after the swap: a complete reset-password round trip.
        var (email, _) = await api.CreateClient().NewClient();
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).EnsureSuccessStatusCode();
        await using var again = api.Services.CreateAsyncScope();
        var payload = (await again.ServiceProvider.GetRequiredService<OttomanDbContext>().OutboxMessages.AsNoTracking()
            .Where(m => m.Type == "PasswordResetRequested").OrderByDescending(m => m.Id).Select(m => m.PayloadJson).Take(50).ToListAsync())
            .First(p => p.Contains(email));
        var token = System.Text.Json.JsonDocument.Parse(payload).RootElement.GetProperty("Token").GetString()!;
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/reset-password", new { email, token, newPassword = "N0va_Lozinka!" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task With_nothing_in_plain_text_the_migration_changes_nothing()
    {
        await using var scope = api.Services.CreateAsyncScope();

        var removed = await scope.ServiceProvider.GetRequiredService<StoredKeyEncryption>().EnsureEncryptedAsync(default);

        removed.Should().Be(0);
    }

    [Fact]
    public void The_encryptor_round_trips_and_every_encryption_is_different()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var secret = new XElement("key", new XElement("masterKey", "very-secret-material"));
        var encryptor = new AesXmlEncryptor(key);

        var a = encryptor.Encrypt(secret);
        var b = encryptor.Encrypt(secret);

        a.EncryptedElement.ToString().Should().NotContain("very-secret-material");
        a.EncryptedElement.ToString().Should().NotBe(b.EncryptedElement.ToString(), "a fresh nonce each time");
        a.DecryptorType.Should().Be(typeof(AesXmlDecryptor));

        var decryptor = Decryptor(Convert.ToBase64String(key));
        decryptor.Decrypt(a.EncryptedElement).ToString().Should().Be(secret.ToString());
    }

    [Fact]
    public void A_wrong_secret_or_tampered_data_does_not_decrypt()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var encrypted = new AesXmlEncryptor(key).Encrypt(new XElement("key", "x")).EncryptedElement;

        var wrong = Decryptor(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        FluentActions.Invoking(() => wrong.Decrypt(encrypted)).Should().Throw<CryptographicException>();

        var blob = Convert.FromBase64String((string)encrypted.Element(AesXmlEncryptor.Ns + "value")!);
        blob[^1] ^= 0x01;
        var tampered = new XElement(AesXmlEncryptor.Ns + "encryptedKey", new XElement(AesXmlEncryptor.Ns + "value", Convert.ToBase64String(blob)));
        FluentActions.Invoking(() => Decryptor(Convert.ToBase64String(key)).Decrypt(tampered)).Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("%%%")]
    [InlineData("c2hvcnQ=")]
    public void A_malformed_secret_is_reported_clearly(string secret) =>
        FluentActions.Invoking(() => new KeyEncryptionOptions { KeyEncryptionSecret = secret }.ParsedKey())
            .Should().Throw<InvalidOperationException>().WithMessage("*32 bytes*");

    private static AesXmlDecryptor Decryptor(string secret)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<KeyEncryptionOptions>>(Options.Create(new KeyEncryptionOptions { KeyEncryptionSecret = secret }));
        return new AesXmlDecryptor(services.BuildServiceProvider());
    }
}
