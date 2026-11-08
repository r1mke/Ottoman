using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Accounts;
using Ottoman.Application.Common;

namespace Ottoman.Api.Controllers;

/// <summary>Admin actions on any client's or barber's account. Every call is written to the audit log by the filter.</summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = Policies.AdminOnly)]
public class UsersController(IAccountAdmin accounts) : ControllerBase
{
    /// <summary>Ends a sign-in lock (too many wrong passwords) before its 15 minutes are over.</summary>
    [HttpPost("{id}/unlock")]
    public async Task<ActionResult<AccountLockDto>> Unlock(string id, CancellationToken ct) =>
        this.ToActionResult(await accounts.UnlockAsync(id, ct));

    /// <summary>
    /// Sets a new password without the old one ("Izmijeni lozinku" in the edit dialog). The audit log records who did
    /// it to which account, never the password.
    /// </summary>
    [HttpPost("{id}/password")]
    public async Task<IActionResult> SetPassword(string id, AdminSetPasswordRequest request, CancellationToken ct) =>
        this.ToNoContent(await accounts.SetPasswordAsync(id, request.NewPassword, ct));
}
