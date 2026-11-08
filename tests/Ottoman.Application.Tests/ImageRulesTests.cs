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

    // A real 1x1 PNG and a real 1x1 lossless WebP.
    private static readonly byte[] RealPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
    private static readonly byte[] RealWebpLossless = Convert.FromBase64String("UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==");

    /// <summary>SOI, a baseline frame of 1x1, a scan with one byte of data, EOI.</summary>
    private static readonly byte[] SmallJpeg =
    [
        0xFF, 0xD8,
        0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
        0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0x00,
        0xFF, 0xD9,
    ];

    private static readonly byte[] LossyWebp =
        [.. "RIFF"u8, 22, 0, 0, 0, .. "WEBP"u8, .. "VP8 "u8, 10, 0, 0, 0, 0x30, 0x01, 0x00, 0x9D, 0x01, 0x2A, 0x01, 0x00, 0x01, 0x00];

    [Fact]
    public void Whole_pictures_are_intact()
    {
        ImageRules.IsIntact(RealPng).Should().BeTrue();
        ImageRules.IsIntact(RealWebpLossless).Should().BeTrue();
        ImageRules.IsIntact(LossyWebp).Should().BeTrue();
        ImageRules.IsIntact(SmallJpeg).Should().BeTrue();
        ImageRules.IsIntact([.. SmallJpeg, 0, 0, 0]).Should().BeTrue("bytes after EOI are tolerated");
        ImageRules.CheckFile(RealPng, 5_000_000).Should().BeNull();
    }

    [Fact]
    public void Headers_and_cut_off_files_are_not()
    {
        ImageRules.IsIntact(Png).Should().BeFalse("only the PNG signature");
        ImageRules.IsIntact(RealPng[..20]).Should().BeFalse("PNG cut to 20 bytes");
        ImageRules.IsIntact(RealPng[..^12]).Should().BeFalse("PNG without IEND");
        ImageRules.IsIntact([.. "RIFF"u8, 100, 0, 0, 0, .. "WEBP"u8]).Should().BeFalse("WebP header only");
        ImageRules.IsIntact(Webp).Should().BeFalse("RIFF size of zero");
        ImageRules.IsIntact(RealWebpLossless[..^3]).Should().BeFalse("WebP cut short");
        ImageRules.IsIntact(Jpeg).Should().BeFalse("JPEG header only");
        ImageRules.IsIntact(SmallJpeg[..^2]).Should().BeFalse("JPEG without EOI");
        ImageRules.IsIntact("not an image"u8).Should().BeFalse();
        ImageRules.CheckFile(RealPng[..20], 5_000_000).Should().Be(ImageRules.CorruptMessage);
    }

    [Fact]
    public void A_flipped_bit_in_a_png_chunk_fails_its_crc()
    {
        var damaged = RealPng.ToArray();
        damaged[45] ^= 0x01; // inside the IDAT data
        ImageRules.IsIntact(damaged).Should().BeFalse();
    }

    [Fact]
    public void A_picture_without_a_size_is_not_intact()
    {
        var zeroWidth = SmallJpeg.ToArray();
        zeroWidth[9] = 0; zeroWidth[10] = 0;
        ImageRules.IsIntact(zeroWidth).Should().BeFalse();
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
