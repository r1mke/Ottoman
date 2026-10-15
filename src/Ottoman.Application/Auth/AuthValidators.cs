using FluentValidation;

namespace Ottoman.Application.Auth;

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
        RuleFor(x => x.Phone).MaximumLength(30).Matches(@"^[0-9+()\-\s/]*$")
            .When(x => !string.IsNullOrEmpty(x.Phone)).WithName("Telefon");
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
        RuleFor(x => x.Phone).MaximumLength(30).Matches(@"^[0-9+()\-\s/]*$")
            .When(x => !string.IsNullOrEmpty(x.Phone)).WithName("Telefon");
    }
}
