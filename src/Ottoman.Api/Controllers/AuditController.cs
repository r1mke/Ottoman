using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Audit;
using Ottoman.Application.Common;

namespace Ottoman.Api.Controllers;

/// <summary>The trail of staff changes. Read-only, admin only; nothing here can edit or delete an entry.</summary>
[ApiController]
[Route("api/v1/audit")]
[Authorize(Policy = Policies.AdminOnly)]
public class AuditController(IAuditQueries audit) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditEntryDto>> List(
        [FromQuery] string? search,
        [FromQuery] string? userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default) =>
        audit.ListAsync(new AuditQuery(search, userId, from?.ToUniversalTime(), to?.ToUniversalTime()), new PageQuery(page, pageSize), ct);
}
