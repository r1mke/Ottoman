namespace Ottoman.Application.Auth;

public record RegisterRequest(string FirstName, string LastName, string Email, string? Phone, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UpdateProfileRequest(string FirstName, string LastName, string? Phone);

/// <summary>How close the caller's current picture crops around the face, 1 (Cloudinary's default) to 2.</summary>
public record UpdatePhotoZoomRequest(double? PhotoZoom);

public record UserDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    IReadOnlyList<string> Roles,
    string? PhotoUrl = null,
    string? Gender = null,
    string? Bio = null,
    double? PhotoZoom = null);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresUtc,
    UserDto User);

public enum AuthFailure
{
    InvalidCredentials,
    InvalidToken,
    ValidationFailed,

    /// <summary>The password reset code is wrong, used or expired (or the account cannot reset): a 400, not a login failure.</summary>
    ResetFailed,
}

/// <summary>The words the reset flow uses everywhere (mail, app, API): the user pastes a code from the mail.</summary>
public static class PasswordResetTexts
{
    public const string FailedTitle = "Reset lozinke nije uspio.";
    public const string InvalidCode = "Kod za reset lozinke nije važeći ili je istekao.";
}

/// <summary>The login field takes an e-mail address or a user name; user names never contain "@".</summary>
public static class LoginNames
{
    public static bool IsEmail(string login) => login.Contains('@');
}

/// <summary>E-mail addresses are stored trimmed and in lower case; lookups go through Identity's normalised (upper) form.</summary>
public static class EmailAddresses
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

/// <summary>Outcome of an auth operation. Failures carry only a generic, non-revealing reason.</summary>
public class AuthResult<T>
{
    public T? Value { get; init; }
    public AuthFailure? Failure { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public bool Succeeded => Failure is null;

    public static AuthResult<T> Ok(T value) => new() { Value = value };
    public static AuthResult<T> Fail(AuthFailure failure, params string[] errors) =>
        new() { Failure = failure, Errors = errors };
}
