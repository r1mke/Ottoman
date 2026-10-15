using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Settings;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController(ISalonSettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<SalonSettingsDto> Get(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<SalonSettingsDto> Update(UpdateSalonSettingsRequest request, CancellationToken ct) =>
        settings.UpdateAsync(request, ct);
}
