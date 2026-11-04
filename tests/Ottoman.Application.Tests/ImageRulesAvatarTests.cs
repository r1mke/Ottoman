using FluentAssertions;
using Ottoman.Application.Images;

namespace Ottoman.Application.Tests;

public class ImageRulesAvatarTests
{
    [Theory]
    [InlineData(1.0, "z_1,")]
    [InlineData(2.0, "z_0.5,")]
    [InlineData(1.6, "z_0.63,")]
    [InlineData(5.0, "z_0.5,")]   // out of range is clamped, never sent to Cloudinary as is
    [InlineData(0.2, "z_1,")]
    public void Zoom_one_to_two_becomes_the_inverse_cloudinary_value(double zoom, string expected)
    {
        var url = ImageRules.AvatarUrl("demo", "ottoman/employees/a", zoom);

        url.Should().StartWith("https://res.cloudinary.com/demo/image/upload/c_thumb,g_face,w_512,h_512,");
        url.Should().Contain(expected).And.EndWith(",q_auto,f_auto/ottoman/employees/a");
    }

    [Fact]
    public void No_key_or_no_account_means_no_url()
    {
        ImageRules.AvatarUrl("demo", null, 1).Should().BeNull();
        ImageRules.AvatarUrl("demo", " ", 1).Should().BeNull();
        ImageRules.AvatarUrl("", "k", 1).Should().BeNull();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1.0, true)]
    [InlineData(2.0, true)]
    [InlineData(0.99, false)]
    [InlineData(2.01, false)]
    [InlineData(double.NaN, false)]
    public void Only_one_to_two_is_a_valid_zoom(double? zoom, bool valid) =>
        ImageRules.ValidZoom(zoom).Should().Be(valid);

    [Theory]
    [InlineData(null, true, null)]
    [InlineData("", true, null)]
    [InlineData("  ", true, null)]
    [InlineData("1", true, 1.0)]
    [InlineData("1.5", true, 1.5)]
    [InlineData(" 2 ", true, 2.0)]
    [InlineData("1,5", false, null)]     // never the local format: "1,5" would silently become 15 in some cultures
    [InlineData("0.99", false, null)]
    [InlineData("2.01", false, null)]
    [InlineData("NaN", false, null)]
    [InlineData("Infinity", false, null)]
    [InlineData("abc", false, null)]
    public void A_zoom_form_field_is_blank_or_an_invariant_number_from_one_to_two(string? raw, bool ok, double? expected)
    {
        ImageRules.TryParseZoom(raw, out var zoom).Should().Be(ok);
        zoom.Should().Be(expected);
    }

    [Fact]
    public void A_zoom_form_field_reads_the_same_whatever_the_server_culture()
    {
        var before = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("bs-Latn-BA");
            ImageRules.TryParseZoom("1.25", out var zoom).Should().BeTrue();
            zoom.Should().Be(1.25);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = before;
        }
    }
}
