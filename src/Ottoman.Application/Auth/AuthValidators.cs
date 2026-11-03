using FluentValidation;

using System.Text.RegularExpressions;

namespace Ottoman.Application.Auth;

public static partial class PhoneRules
{
    // National number after 0 / +387 / 00387: mobile 060-067 (+ 6 or 7 digits), landline 03x, 049, 05x (+ 6 digits).
    [GeneratedRegex(@"^(?:\+387|00387|0)(?:6[0-7]\d{6,7}|(?:3\d|49|5\d)\d{6})$")]
    private static partial Regex Bosnian();

    /// <summary>Optional phone in the Bosnian format; spaces, dashes, slashes and brackets are allowed as separators.</summary>
    public static IRuleBuilderOptions<T, string?> BosnianPhone<T>(this IRuleBuilder<T, string?> rule) => rule
        .MaximumLength(30)
        .Must(p => string.IsNullOrEmpty(p) || Bosnian().IsMatch(Regex.Replace(p.Trim(), @"[\s/().\-]", "")))
        .WithMessage("Unesite ispravan broj telefona, npr. 061 123 456 ili +387 61 123 456.");
}

public static class PasswordRules
{
    /// <summary>Mirrors the Identity password options so the client gets a 400 with field errors, not a generic failure.</summary>
    public static IRuleBuilderOptions<T, string> Strong<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Lozinka je obavezna.")
        .MinimumLength(8).WithMessage("Lozinka mora imati najmanje 8 znakova.")
        .MaximumLength(128).WithMessage("Lozinka je predugačka.")
        .Matches("[a-z]").WithMessage("Lozinka mora sadržavati malo slovo.")
        .Matches("[A-Z]").WithMessage("Lozinka mora sadržavati veliko slovo.")
        .Matches("[0-9]").WithMessage("Lozinka mora sadržavati broj.")
        .Matches("[^a-zA-Z0-9]").WithMessage("Lozinka mora sadržavati poseban znak.");
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithName("Ime");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithName("Prezime");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256).WithName("Email");
        RuleFor(x => x.Phone).BosnianPhone().WithName("Telefon");
        RuleFor(x => x.Password).Strong();
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.NewPassword).Strong();
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).Strong();
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithName("Ime");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithName("Prezime");
        RuleFor(x => x.Phone).BosnianPhone().WithName("Telefon");
    }
}
