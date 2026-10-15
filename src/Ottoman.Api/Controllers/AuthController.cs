using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ottoman.Api.Auth;
using Ottoman.Application.Auth;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Creates a client account. Always 202 for valid input so existing accounts are not revealed; log in afterwards.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.RegisterLimit)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return result.Succeeded ? Accepted() : Problem(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.LoginLimit)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        return result.Succeeded ? result.Value! : Problem(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.RefreshLimit)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(request.RefreshToken, ct);
        return result.Succeeded ? result.Value! : Problem(result);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.RefreshLimit)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>Always 204, whether or not the account exists.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.ForgotLimit)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request.Email, ct);
        return NoContent();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(Policies.ForgotLimit)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ResetPasswordAsync(request, ct);
        return result.Succeeded ? NoContent() : Problem(result);
    }

    private ObjectResult Problem<T>(AuthResult<T> result)
    {
        var detail = result.Errors.FirstOrDefault() ?? "Zahtjev nije uspio.";
        return result.Failure switch
        {
            AuthFailure.ValidationFailed => new BadRequestObjectResult(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["request"] = result.Errors.ToArray() })
            {
                Title = "Podaci nisu ispravni.",
                Status = StatusCodes.Status400BadRequest,
            }),
            _ => new ObjectResult(new ProblemDetails
            {
                Title = "Neuspješna prijava.",
                Detail = detail,
                Status = StatusCodes.Status401Unauthorized,
            })
            { StatusCode = StatusCodes.Status401Unauthorized },
        };
    }
}
