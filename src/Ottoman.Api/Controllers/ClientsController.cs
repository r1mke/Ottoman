using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Clients;
using Ottoman.Application.Common;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/clients")]
[Authorize(Policy = Policies.AdminOnly)]
public class ClientsController(IClientQueries clients) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ClientListItem>> List(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        clients.ListAsync(search, new PageQuery(page, pageSize), ct);
}
