using FluentAssertions;
using Ottoman.Application.Auth;
using Ottoman.Application.Common;

namespace Ottoman.Application.Tests;

public class TextSafetyTests
{
    [Theory]
    [InlineData("Ana Anić", true)]
    [InlineData("Prvi red\r\nDrugi\tred", true)]
    [InlineData("Šišanje ✂ 😀", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("a\u0000b", false)]
    [InlineData("a\u0001", false)]
    [InlineData("a\u007F", false)]
    [InlineData("gpj.‮exe", false)]
    [InlineData("‪", false)]
    [InlineData("⁦x⁩", false)]
    public void Only_control_and_bidi_override_characters_are_refused(string? text, bool safe) =>
        TextSafety.IsSafe(text).Should().Be(safe);

    [Theory]
    [InlineData("  Ana.Anic@Ottoman.TEST ", "ana.anic@ottoman.test")]
    [InlineData("x@y.ba", "x@y.ba")]
    public void Emails_are_trimmed_and_lower_cased(string raw, string stored) =>
        EmailAddresses.Normalize(raw).Should().Be(stored);
}
