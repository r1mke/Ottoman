using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application.Common;
using Ottoman.Application.Images;
using Ottoman.Application.Salon;

namespace Ottoman.Api.Controllers;

/// <summary>What the home screen shows about the salon: name, description, logo, address, map link and opening hours.</summary>
[ApiController]
[Route("api/v1/salon")]
[Authorize]
public class SalonController(ISalonProfileService salon, IImageAttachments images) : ControllerBase
{
    [HttpGet]
    public Task<SalonProfileDto> Get(CancellationToken ct) => salon.GetAsync(ct);

    [HttpPut]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<SalonProfileDto> Update(UpdateSalonProfileRequest request, CancellationToken ct) => salon.UpdateAsync(request, ct);

    [HttpPut("logo")]
    [Authorize(Policy = Policies.AdminOnly)]
    [RejectOversizedUpload]
    [RequestSizeLimit(ImageUpload.RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUpload.FormLimit)]
    public async Task<ActionResult<SalonProfileDto>> SetLogo(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var set = await images.SetSalonLogoAsync(stream, ct);
        return set.Succeeded ? await salon.GetAsync(ct) : this.ToActionResult(new Result<SalonProfileDto>(set.Status, default, set.Error));
    }

    [HttpDelete("logo")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<SalonProfileDto>> ClearLogo(CancellationToken ct)
    {
        var cleared = await images.ClearSalonLogoAsync(ct);
        return cleared.Succeeded ? await salon.GetAsync(ct) : this.ToActionResult(new Result<SalonProfileDto>(cleared.Status, default, cleared.Error));
    }
}
