using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Catalog;
using Ottoman.Domain;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/services")]
[Authorize]
public class ServicesController(IServiceCatalog catalog) : ControllerBase
{
    /// <summary>Active services; with <c>employeeId</c> only those that employee performs. Admins may include inactive ones.</summary>
    [HttpGet]
    public Task<IReadOnlyList<ServiceDto>> List(
        [FromQuery] string? employeeId, [FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        catalog.ListAsync(employeeId, includeInactive && User.IsInRole(Roles.Admin), ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ServiceDto>> Get(int id, CancellationToken ct) =>
        this.ToActionResult(await catalog.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ServiceDto>> Create(ServiceUpsertRequest request, CancellationToken ct)
    {
        var result = await catalog.CreateAsync(request, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ServiceDto>> Update(int id, ServiceUpsertRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct) =>
        this.ToNoContent(await catalog.DeactivateAsync(id, ct));
}
