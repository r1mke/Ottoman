using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Auth;

namespace Ottoman.Api.Controllers;

/// <summary>The caller's own account. The user id always comes from the token, never from the request.</summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MeController(IAuthService auth) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserDto>> Get(CancellationToken ct)
    {
        var me = await auth.GetProfileAsync(User.UserId(), ct);
        return me is null ? NotFound() : me;
    }

    [HttpPut]
    public async Task<ActionResult<UserDto>> Update(UpdateProfileRequest request, CancellationToken ct)
    {
        var me = await auth.UpdateProfileAsync(User.UserId(), request, ct);
        return me is null ? NotFound() : me;
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await auth.ChangePasswordAsync(User.UserId(), request, ct);
        if (result.Succeeded) return NoContent();
        return BadRequest(new ValidationProblemDetails(
            new Dictionary<string, string[]> { ["request"] = result.Errors.ToArray() })
        {
            Title = "Podaci nisu ispravni.",
            Status = StatusCodes.Status400BadRequest,
        });
    }
}
