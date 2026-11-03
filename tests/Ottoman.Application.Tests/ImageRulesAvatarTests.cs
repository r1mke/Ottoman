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
}
