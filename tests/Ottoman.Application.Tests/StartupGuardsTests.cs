using FluentAssertions;
using Ottoman.Application.Common;

namespace Ottoman.Application.Tests;

public class StartupGuardsTests
{
    private const string RealKey = "test-only-random-signing-key-0123456789-abcdefghij-XYZ";

    [Fact]
    public void Development_may_use_anything() =>
        StartupGuards.Problems(true, "change-me", true, "ChangeMe_Admin1!").Should().BeEmpty();

    [Fact]
    public void A_real_production_configuration_passes() =>
        StartupGuards.Problems(false, RealKey, false, "Zq9!vKd02mXr").Should().BeEmpty();

    [Theory]
    [InlineData("change-me-change-me-change-me-change-me-1234")]
    [InlineData("MyPasswordIsVerySecretAndLongEnough123456")]
    [InlineData("your-key-goes-here-your-key-goes-here-12")]
    public void Placeholder_keys_are_refused_in_production(string key) =>
        StartupGuards.Problems(false, key, false, null).Should().ContainSingle(p => p.Contains("placeholder"));

    [Fact]
    public void A_key_with_no_variety_is_refused() =>
        StartupGuards.Problems(false, new string('a', 64), false, null).Should().ContainSingle(p => p.Contains("variety"));

    [Fact]
    public void Demo_data_is_refused_in_production() =>
        StartupGuards.Problems(false, RealKey, true, null).Should().ContainSingle(p => p.Contains("DemoData"));

    [Fact]
    public void A_placeholder_admin_password_is_refused_but_an_unset_one_is_fine()
    {
        StartupGuards.Problems(false, RealKey, false, "ChangeMe_Admin1!").Should().ContainSingle(p => p.Contains("AdminPassword"));
        StartupGuards.Problems(false, RealKey, false, null).Should().BeEmpty();
        StartupGuards.Problems(false, RealKey, false, "").Should().BeEmpty();
    }

    [Fact]
    public void All_problems_are_reported_at_once() =>
        StartupGuards.Problems(false, "change-me", true, "changeme1").Should().HaveCount(3);
}
