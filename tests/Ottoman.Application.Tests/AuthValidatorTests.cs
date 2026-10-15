using FluentAssertions;
using Ottoman.Application.Auth;

namespace Ottoman.Application.Tests;

public class AuthValidatorTests
{
    private readonly RegisterRequestValidator _register = new();

    private static RegisterRequest Valid() =>
        new("Kerim", "Begić", "kerim@example.com", "+387 61 123 456", "Str0ng_Pass!");

    [Fact]
    public void Valid_registration_passes() => _register.Validate(Valid()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("", "Begić", "a@b.com")]
    [InlineData("Kerim", "", "a@b.com")]
    [InlineData("Kerim", "Begić", "not-an-email")]
    [InlineData("Kerim", "Begić", "")]
    public void Invalid_names_or_email_fail(string first, string last, string email) =>
        _register.Validate(Valid() with { FirstName = first, LastName = last, Email = email })
            .IsValid.Should().BeFalse();

    [Theory]
    [InlineData("Sh0rt!")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoDigits!!")]
    [InlineData("NoSymbols123")]
    public void Weak_passwords_fail(string password) =>
        _register.Validate(Valid() with { Password = password }).IsValid.Should().BeFalse();

    [Fact]
    public void Overlong_password_fails_to_avoid_hashing_abuse() =>
        _register.Validate(Valid() with { Password = new string('a', 200) + "A1!" }).IsValid.Should().BeFalse();

    [Theory]
    [InlineData("abc")]
    [InlineData("<script>")]
    public void Phone_rejects_non_phone_characters(string phone) =>
        _register.Validate(Valid() with { Phone = phone }).IsValid.Should().BeFalse();

    [Fact]
    public void Phone_is_optional() =>
        _register.Validate(Valid() with { Phone = null }).IsValid.Should().BeTrue();

    [Fact]
    public void Reset_password_requires_strong_new_password() =>
        new ResetPasswordRequestValidator()
            .Validate(new ResetPasswordRequest("a@b.com", "token", "weak")).IsValid.Should().BeFalse();
}
