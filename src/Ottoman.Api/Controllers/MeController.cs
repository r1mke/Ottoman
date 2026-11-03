using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application.Auth;
using Ottoman.Application.Employees;
using Ottoman.Application.Images;

namespace Ottoman.Api.Controllers;

/// <summary>The caller's own account. The user id always comes from the token, never from the request.</summary>
[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MeController(IAuthService auth, IEmployeeService employees, IImageAttachments images) : ControllerBase
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

    // ----- a barber's (or the admin's) own picture and profile. The id is the caller's; nobody can touch another barber's here.

    [HttpPut("employee-profile")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<UserDto>> UpdateEmployeeProfile(UpdateOwnEmployeeProfileRequest request, CancellationToken ct) =>
        await Fresh(await employees.UpdateOwnProfileAsync(User.UserId(), request, ct), ct);

    [HttpPut("photo")]
    [Authorize(Policy = Policies.Staff)]
    [RejectOversizedUpload]
    [RequestSizeLimit(ImageUpload.RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUpload.FormLimit)]
    public async Task<ActionResult<UserDto>> SetPhoto(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var set = await images.SetEmployeePhotoAsync(User.UserId(), stream, ct, onAccount: true);
        return await Fresh(set, ct);
    }

    [HttpDelete("photo")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<UserDto>> ClearPhoto(CancellationToken ct)
    {
        var cleared = await images.ClearEmployeePhotoAsync(User.UserId(), ct, onAccount: true);
        return await Fresh(cleared, ct);
    }

    /// <summary>The caller's fresh account after an own-profile change, or the failure as a problem response.</summary>
    private async Task<ActionResult<UserDto>> Fresh<T>(Application.Common.Result<T> step, CancellationToken ct)
    {
        if (!step.Succeeded) return this.ToActionResult(new Application.Common.Result<UserDto>(step.Status, default, step.Error));
        var me = await auth.GetProfileAsync(User.UserId(), ct);
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
