using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Push;

namespace Ottoman.Api.Controllers;

/// <summary>Phones of the signed-in user that may receive push messages. Everything is scoped to the caller.</summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize]
public class DevicesController(IDeviceRegistry devices) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(RegisterDeviceRequest request, CancellationToken ct)
    {
        await devices.RegisterAsync(User.UserId(), request, ct);
        return NoContent();
    }

    /// <summary>Called on sign-out so the next person on this phone does not get the previous person's messages.</summary>
    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister(UnregisterDeviceRequest request, CancellationToken ct)
    {
        await devices.UnregisterAsync(User.UserId(), request.Token, ct);
        return NoContent();
    }
}

public record UnregisterDeviceRequest(string Token);
