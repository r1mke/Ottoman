using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application.Common;
using Ottoman.Application.Gallery;
using Ottoman.Application.Images;

namespace Ottoman.Api.Controllers;

/// <summary>The work gallery. Everybody signed in can browse it; only the admin changes it.</summary>
[ApiController]
[Route("api/v1/gallery")]
[Authorize]
public class GalleryController(IGalleryService gallery, IImageAttachments images) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<GalleryItemDto>> List(
        [FromQuery] int? categoryId, [FromQuery] bool? featured, [FromQuery] string? employeeId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 24, CancellationToken ct = default) =>
        await gallery.PageAsync(categoryId, featured, employeeId, new PageQuery(page, pageSize), ct);

    [HttpGet("categories")]
    public async Task<IReadOnlyList<HaircutCategoryDto>> Categories(
        [FromQuery] int page = 1, [FromQuery] int pageSize = PageQuery.MaxPageSize, CancellationToken ct = default) =>
        this.Page(await gallery.CategoriesAsync(new PageQuery(page, pageSize), ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GalleryItemDto>> Get(int id, CancellationToken ct) =>
        this.ToActionResult(await gallery.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    [RejectOversizedUpload]
    [RequestSizeLimit(ImageUpload.RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUpload.FormLimit)]
    public async Task<ActionResult<GalleryItemDto>> Add(
        IFormFile file, [FromForm] int categoryId, [FromForm] string? employeeId, [FromForm] string? caption,
        [FromForm] bool isFeatured, CancellationToken ct)
    {
        if (caption is { Length: > 200 })
            return this.ToActionResult(Result<GalleryItemDto>.Invalid("Opis može imati najviše 200 znakova."));

        await using var stream = file.OpenReadStream();
        var added = await images.AddGalleryItemAsync(
            categoryId, string.IsNullOrWhiteSpace(employeeId) ? null : employeeId, caption, isFeatured, stream, ct);
        if (!added.Succeeded)
            return this.ToActionResult(new Result<GalleryItemDto>(added.Status, default, added.Error));

        var item = await gallery.GetAsync(added.Value, ct);
        return item.Succeeded ? Created($"api/v1/gallery/{added.Value}", item.Value) : this.ToActionResult(item);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<GalleryItemDto>> Update(int id, UpdateGalleryItemRequest request, CancellationToken ct) =>
        this.ToActionResult(await gallery.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToNoContent(await images.RemoveGalleryItemAsync(id, ct));

    // ----- haircut styles

    [HttpPost("categories")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<HaircutCategoryDto>> CreateCategory(HaircutCategoryRequest request, CancellationToken ct)
    {
        var result = await gallery.CreateCategoryAsync(request, ct);
        return result.Succeeded ? Created($"api/v1/gallery/categories/{result.Value!.Id}", result.Value) : this.ToActionResult(result);
    }

    [HttpPut("categories/{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<HaircutCategoryDto>> RenameCategory(int id, HaircutCategoryRequest request, CancellationToken ct) =>
        this.ToActionResult(await gallery.RenameCategoryAsync(id, request, ct));

    [HttpDelete("categories/{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct) =>
        this.ToNoContent(await gallery.DeleteCategoryAsync(id, ct));
}
