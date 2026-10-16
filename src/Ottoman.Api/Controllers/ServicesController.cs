using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application.Catalog;
using Ottoman.Application.Common;
using Ottoman.Application.Images;
using Ottoman.Domain;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/services")]
[Authorize]
public class ServicesController(IServiceCatalog catalog, IImageAttachments images) : ControllerBase
{
    /// <summary>Active services; with <c>employeeId</c> only those that employee performs. Admins may include inactive ones.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<ServiceDto>> List(
        [FromQuery] string? employeeId,
        [FromQuery] string? search,
        [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PageQuery.MaxPageSize,
        CancellationToken ct = default) =>
        this.Page(await catalog.PageAsync(
            employeeId, includeInactive && User.IsInRole(Roles.Admin), search, new PageQuery(page, pageSize), ct));

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

    [HttpPut("{id:int}/image")]
    [Authorize(Policy = Policies.AdminOnly)]
    [RejectOversizedUpload]
    [RequestSizeLimit(ImageUpload.RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUpload.FormLimit)]
    public async Task<ActionResult<ServiceDto>> SetImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var set = await images.SetServiceImageAsync(id, stream, ct);
        return this.ToActionResult(set.Succeeded ? await catalog.GetAsync(id, ct) : set.As<ServiceDto>());
    }

    [HttpDelete("{id:int}/image")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ServiceDto>> ClearImage(int id, CancellationToken ct)
    {
        var cleared = await images.ClearServiceImageAsync(id, ct);
        return this.ToActionResult(cleared.Succeeded ? await catalog.GetAsync(id, ct) : cleared.As<ServiceDto>());
    }
}
