using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ottoman.Application.Auth;
using Ottoman.Application.Common;
using Ottoman.Application.Notifications;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Identity;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Auth;

public class AuthService(
    OttomanDbContext db,
    UserManager<AppUser> users,
    ITokenService tokens,
    IOutbox outbox,
    IOptions<JwtOptions> jwt,
    IResetTokenVault vault,
    IAccessGate gate,
    TimeProvider clock) : IAuthService
{
    private const string GenericLoginError = "Neispravan email ili lozinka.";
    private const string GenericTokenError = "Sesija je istekla. Prijavite se ponovo.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- register

    public async Task<AuthResult<bool>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var fullName = $"{request.FirstName} {request.LastName}".Trim();

        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            outbox.Enqueue(new RegistrationAttemptedForExistingAccount(existing.Email!, existing.FullName));
            await db.SaveChangesAsync(ct);
            return AuthResult<bool>.Ok(true); // indistinguishable from a fresh registration
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            RegisteredAtUtc = Now,
        };

        // The role is fixed here, never taken from the request.
        var strategy = db.Database.CreateExecutionStrategy();
        IdentityResult? failure = null;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var created = await users.CreateAsync(user, request.Password);
            if (!created.Succeeded)
            {
                failure = created;
                return;
            }
            await users.AddToRoleAsync(user, Roles.Client);
            outbox.Enqueue(new UserRegistered(user.Id, user.Email!, fullName));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        if (failure is null) return AuthResult<bool>.Ok(true);

        var duplicate = failure.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName");
        if (duplicate) return AuthResult<bool>.Ok(true); // lost a race with another registration

        return AuthResult<bool>.Fail(AuthFailure.ValidationFailed, failure.Errors.Select(e => e.Description).ToArray());
    }

    // ------------------------------------------------------------------- login

    public async Task<AuthResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Burn comparable CPU so response time does not reveal whether the account exists.
            users.PasswordHasher.HashPassword(new AppUser(), request.Password);
            return AuthResult<AuthResponse>.Fail(AuthFailure.InvalidCredentials, GenericLoginError);
        }

        if (user.IsBlocked || await users.IsLockedOutAsync(user))
            return AuthResult<AuthResponse>.Fail(AuthFailure.InvalidCredentials, GenericLoginError);

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return AuthResult<AuthResponse>.Fail(AuthFailure.InvalidCredentials, GenericLoginError);
        }

        await users.ResetAccessFailedCountAsync(user);
        return AuthResult<AuthResponse>.Ok((await IssueAsync(user, Guid.NewGuid(), ct)).Response);
    }

    // ----------------------------------------------------------------- refresh

    public async Task<AuthResult<AuthResponse>> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokens.Hash(refreshToken);
        var stored = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null)
            return AuthResult<AuthResponse>.Fail(AuthFailure.InvalidToken, GenericTokenError);

        if (stored.RevokedUtc is not null)
        {
            // A rotated token was presented again: assume theft and end every session of this user.
            await RevokeAllAsync(stored.UserId, ct);
            return AuthResult<AuthResponse>.Fail(AuthFailure.InvalidToken, GenericTokenError);
        }

        if (stored.ExpiresUtc <= Now)
            return AuthResult<AuthResponse>.Fail(AuthFailure.InvalidToken, GenericTokenError);

        var user = await users.FindByIdAsync(stored.UserId);
        if (user is null || user.IsBlocked)
            return AuthResult<AuthResponse>.Fail(AuthFailure.InvalidToken, GenericTokenError);

        var (response, newHash) = await IssueAsync(user, stored.SessionId, ct);

        // Atomic single-use: only one concurrent refresh can flip RevokedUtc from NULL.
        var now = Now;
        var claimed = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedUtc == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedUtc, now)
                .SetProperty(t => t.ReplacedByHash, newHash), ct);

        if (claimed == 0)
        {
            await RevokeAllAsync(stored.UserId, ct);
            return AuthResult<AuthResponse>.Fail(AuthFailure.InvalidToken, GenericTokenError);
        }

        return AuthResult<AuthResponse>.Ok(response);
    }

    /// <summary>
    /// Ends the whole sign-in on the server: the refresh token and, because the access token names its session, the
    /// access token too. Signing out only on the phone would leave a copied token working for up to 15 minutes.
    /// </summary>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = tokens.Hash(refreshToken);
        var session = await db.RefreshTokens.AsNoTracking().Where(t => t.TokenHash == hash)
            .Select(t => new { t.SessionId, t.UserId }).FirstOrDefaultAsync(ct);
        if (session is null) return;

        var now = Now;
        await db.RefreshTokens
            .Where(t => t.SessionId == session.SessionId && t.RevokedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedUtc, now), ct);
        gate.Invalidate(session.UserId);
    }

    // ---------------------------------------------------------- password reset

    public async Task ForgotPasswordAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || user.IsBlocked) return;

        var token = await users.GeneratePasswordResetTokenAsync(user);
        outbox.Enqueue(new PasswordResetRequested(user.Id, user.Email!, user.FullName, vault.Seal(token)));
        await db.SaveChangesAsync(ct);
    }

    public async Task<AuthResult<bool>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.IsBlocked)
            return AuthResult<bool>.Fail(AuthFailure.InvalidToken, "Link za reset lozinke nije važeći ili je istekao.");

        // Identity binds the token to the security stamp, which this call rotates: the token is single-use.
        var result = await users.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return AuthResult<bool>.Fail(AuthFailure.InvalidToken, "Link za reset lozinke nije važeći ili je istekao.");

        await users.ResetAccessFailedCountAsync(user);
        await RevokeAllAsync(user.Id, ct);
        return AuthResult<bool>.Ok(true);
    }

    public async Task<AuthResult<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return AuthResult<bool>.Fail(AuthFailure.InvalidToken, GenericTokenError);

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return AuthResult<bool>.Fail(AuthFailure.ValidationFailed, result.Errors.Select(e => e.Description).ToArray());

        await RevokeAllAsync(user.Id, ct);
        return AuthResult<bool>.Ok(true);
    }

    // ----------------------------------------------------------------- profile

    public async Task<UserDto?> GetProfileAsync(string userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId);
        return user is null ? null : await ToDtoAsync(user);
    }

    public async Task<UserDto?> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null) return null;

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        await users.UpdateAsync(user);
        return await ToDtoAsync(user);
    }

    // ----------------------------------------------------------------- helpers

    private async Task<(AuthResponse Response, string RefreshHash)> IssueAsync(AppUser user, Guid sessionId, CancellationToken ct)
    {
        var dto = await ToDtoAsync(user);
        var (access, expires) = tokens.CreateAccessToken(user.Id, user.Email!, dto.Roles, sessionId);

        var raw = tokens.CreateRefreshToken();
        var refreshHash = tokens.Hash(raw);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            SessionId = sessionId,
            ExpiresUtc = Now.AddDays(jwt.Value.RefreshTokenDays),
        });
        await db.SaveChangesAsync(ct);

        return (new AuthResponse(access, raw, expires, dto), refreshHash);
    }

    private async Task RevokeAllAsync(string userId, CancellationToken ct)
    {
        var now = Now;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedUtc, now), ct);
        gate.Invalidate(userId);
    }

    private async Task<UserDto> ToDtoAsync(AppUser user) => new(
        user.Id, user.Email!, user.FirstName, user.LastName, user.PhoneNumber,
        (await users.GetRolesAsync(user)).ToList());
}
