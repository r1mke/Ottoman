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

    [HttpGet("{id}")]
    public async Task<ActionResult<ClientDetail>> Get(string id, CancellationToken ct) =>
        this.ToActionResult(await clients.GetAsync(id, ct));

    [HttpPost("{id}/block")]
    public async Task<ActionResult<ClientListItem>> Block(string id, CancellationToken ct) =>
        this.ToActionResult(await clients.SetBlockedAsync(id, true, ct));

    [HttpPost("{id}/unblock")]
    public async Task<ActionResult<ClientListItem>> Unblock(string id, CancellationToken ct) =>
        this.ToActionResult(await clients.SetBlockedAsync(id, false, ct));
}
