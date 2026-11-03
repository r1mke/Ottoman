namespace Ottoman.Application.Auth;

public record RegisterRequest(string FirstName, string LastName, string Email, string? Phone, string Password);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UpdateProfileRequest(string FirstName, string LastName, string? Phone);

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
