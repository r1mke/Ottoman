namespace Ottoman.Application.Auth;

public interface IAuthService
{
    /// <summary>
    /// Creates a Client account. Never reveals whether the email already existed:
    /// the caller always sees the same outcome for valid input.
    /// </summary>
    Task<AuthResult<bool>> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<AuthResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>Rotates the refresh token. Presenting an already-used token revokes the whole session set.</summary>
    Task<AuthResult<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct);

    Task LogoutAsync(string refreshToken, CancellationToken ct);

    /// <summary>Always completes the same way regardless of whether the account exists.</summary>
    Task ForgotPasswordAsync(string email, CancellationToken ct);

    Task<AuthResult<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);

    Task<AuthResult<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct);

    Task<UserDto?> GetProfileAsync(string userId, CancellationToken ct);

    Task<UserDto?> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken ct);
}
