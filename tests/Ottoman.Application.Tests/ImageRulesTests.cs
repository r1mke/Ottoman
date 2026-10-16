using FluentAssertions;
using Ottoman.Application.Images;

namespace Ottoman.Application.Tests;

public class ImageRulesTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0];
    private static readonly byte[] Webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    [Fact]
    public void The_type_comes_from_the_bytes() =>
        new[] { ImageRules.Detect(Png), ImageRules.Detect(Jpeg), ImageRules.Detect(Webp) }
            .Should().Equal("image/png", "image/jpeg", "image/webp");

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>")]
    [InlineData("GIF89a......")]
    [InlineData("MZ executable")]
    [InlineData("")]
    public void Anything_else_is_refused_whatever_the_client_claims(string text) =>
        ImageRules.Detect(System.Text.Encoding.ASCII.GetBytes(text)).Should().BeNull();

    [Fact]
    public void Check_accepts_a_small_real_picture() =>
        ImageRules.Check(Png.Length, Png, 5_000_000).Should().BeNull();

    [Fact]
    public void Check_refuses_empty_oversized_and_fake_files()
    {
        ImageRules.Check(0, [], 5_000_000).Should().Contain("prazna");
        ImageRules.Check(5_000_001, Png, 5_000_000).Should().Contain("prevelika");
        ImageRules.Check(100, "not an image"u8, 5_000_000).Should().Contain("JPEG");
    }

    [Fact]
    public void Delivery_url_is_built_from_the_key_and_capped_in_width()
    {
        ImageRules.DeliveryUrl("demo", "ottoman/products/abc")
            .Should().Be("https://res.cloudinary.com/demo/image/upload/c_limit,w_1200,q_auto,f_auto/ottoman/products/abc");
        ImageRules.DeliveryUrl("demo", null).Should().BeNull();
        ImageRules.DeliveryUrl("", "x").Should().BeNull();
    }
}
