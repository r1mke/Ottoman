using FluentAssertions;
using Ottoman.Application.Common;

namespace Ottoman.Application.Tests;

public class StartupGuardsTests
{
    private const string RealKey = "test-only-random-signing-key-0123456789-abcdefghij-XYZ";

    /// <summary>32 bytes with variety, base64: what openssl rand -base64 32 produces.</summary>
    private static readonly string RealSecret = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)(i * 7)).ToArray());

    [Fact]
    public void Development_may_use_anything() =>
        StartupGuards.Problems(true, "change-me", true, "ChangeMe_Admin1!").Should().BeEmpty();

    [Fact]
    public void A_real_production_configuration_passes() =>
        StartupGuards.Problems(false, RealKey, false, "Zq9!vKd02mXr", RealSecret).Should().BeEmpty();

    [Theory]
    [InlineData("change-me-change-me-change-me-change-me-1234")]
    [InlineData("MyPasswordIsVerySecretAndLongEnough123456")]
    [InlineData("your-key-goes-here-your-key-goes-here-12")]
    public void Placeholder_keys_are_refused_in_production(string key) =>
        StartupGuards.Problems(false, key, false, null, RealSecret).Should().ContainSingle(p => p.Contains("placeholder"));

    [Fact]
    public void A_key_with_no_variety_is_refused() =>
        StartupGuards.Problems(false, new string('a', 64), false, null, RealSecret).Should().ContainSingle(p => p.Contains("variety"));

    [Fact]
    public void Demo_data_is_refused_in_production() =>
        StartupGuards.Problems(false, RealKey, true, null, RealSecret).Should().ContainSingle(p => p.Contains("DemoData"));

    [Fact]
    public void A_placeholder_admin_password_is_refused_but_an_unset_one_is_fine()
    {
        StartupGuards.Problems(false, RealKey, false, "ChangeMe_Admin1!", RealSecret).Should().ContainSingle(p => p.Contains("AdminPassword"));
        StartupGuards.Problems(false, RealKey, false, null, RealSecret).Should().BeEmpty();
        StartupGuards.Problems(false, RealKey, false, "", RealSecret).Should().BeEmpty();
    }

    [Fact]
    public void All_problems_are_reported_at_once() =>
        StartupGuards.Problems(false, "change-me", true, "changeme1").Should().HaveCount(4);

    // ------------------------------------------------- stored-key encryption secret

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not base64 at all!")]
    [InlineData("c2hvcnQ=")]
    public void Production_needs_a_real_key_encryption_secret(string? secret) =>
        StartupGuards.Problems(false, RealKey, false, null, secret).Should().ContainSingle(p => p.Contains("KeyEncryptionSecret"));

    [Fact]
    public void A_secret_of_the_wrong_length_or_no_variety_is_refused()
    {
        StartupGuards.IsValidEncryptionSecret(Convert.ToBase64String(new byte[16])).Should().BeFalse("16 bytes is not enough");
        StartupGuards.IsValidEncryptionSecret(Convert.ToBase64String(new byte[32])).Should().BeFalse("all zeros is not random");
        StartupGuards.IsValidEncryptionSecret(Convert.ToBase64String(new byte[64])).Should().BeFalse("64 bytes is not an AES-256 key");
        StartupGuards.IsValidEncryptionSecret(RealSecret).Should().BeTrue();
    }

    [Fact]
    public void Development_does_not_need_the_secret() =>
        StartupGuards.Problems(true, RealKey, false, null, null).Should().BeEmpty();

    // ------------------------------------------------------------------ cors

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.example.com")]
    [InlineData("http://admin.example.com")]
    [InlineData("admin.example.com")]
    [InlineData("https://admin.example.com/path")]
    public void Bad_cors_origins_stop_a_production_start(string origin) =>
        StartupGuards.Problems(false, RealKey, false, null, RealSecret, [origin]).Should().ContainSingle(p => p.Contains("Cors:Origins"));

    [Fact]
    public void Exact_https_origins_are_fine() =>
        StartupGuards.Problems(false, RealKey, false, null, RealSecret, ["https://admin.example.com", "https://salon.example.com:8443"]).Should().BeEmpty();

    [Fact]
    public void Development_may_use_plain_http_on_localhost_but_not_a_wildcard()
    {
        StartupGuards.Problems(true, RealKey, false, null, null, ["http://localhost:5173"]).Should().BeEmpty();
        StartupGuards.Problems(true, RealKey, false, null, null, ["*"]).Should().ContainSingle(p => p.Contains("wildcard"));
        StartupGuards.Problems(true, RealKey, false, null, null, ["http://example.com"]).Should().ContainSingle();
    }

    [Fact]
    public void An_empty_origin_from_an_unset_environment_variable_is_ignored()
    {
        StartupGuards.Problems(false, RealKey, false, null, RealSecret, [""]).Should().BeEmpty();
        CorsRules.Normalize(["", "  "], false).Should().BeEmpty();
    }

    [Fact]
    public void Origins_are_normalised_without_slash_and_duplicates()
    {
        CorsRules.Normalize(["https://a.example.com/", "https://a.example.com", "https://B.example.com:443"], false)
            .Should().Equal("https://a.example.com", "https://b.example.com");
    }
}
