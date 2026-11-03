using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application.Common;
using Ottoman.Application.Images;
using Ottoman.Application.Shop;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/shop/sections")]
[Authorize]
public class ShopSectionsController(
    IShopSectionService sections, IProductCatalog catalog, IImageAttachments images) : ControllerBase
{
    /// <summary>What the shop entry page shows: categories and brands with counts and pictures, the shortcuts and the section cards.</summary>
    [HttpGet("~/api/v1/shop/overview")]
    public async Task<ShopOverviewDto> Overview(CancellationToken ct)
    {
        var overview = await catalog.GetOverviewAsync(ct);
        return overview with
        {
            Cards = await sections.GetCardsAsync(ct),
            Blocks = await sections.GetBlocksAsync(overview.Categories, overview.Brands, ct),
        };
    }

    /// <summary>The products a section card opens, a page at a time (at most <c>PageQuery.MaxPageSize</c>).</summary>
    [HttpGet("{id:int}/products")]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> Products(
        int id, [FromQuery] int page = 1, [FromQuery] int pageSize = PageQuery.MaxPageSize, CancellationToken ct = default)
    {
        var result = await sections.GetProductsAsync(id, new PageQuery(page, pageSize), ct);
        return result.Succeeded
            ? Ok(this.Page(result.Value!))
            : this.ToActionResult(new Result<IReadOnlyList<ProductDto>>(result.Status, default, result.Error));
    }

    [HttpGet("manage")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IReadOnlyList<ShopSectionAdminDto>> Manage(
        [FromQuery] int page = 1, [FromQuery] int pageSize = PageQuery.MaxPageSize, CancellationToken ct = default) =>
        this.Page(await sections.ListForAdminAsync(new PageQuery(page, pageSize), ct));

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ShopSectionAdminDto>> Create(ShopSectionRequest request, CancellationToken ct)
    {
        var result = await sections.CreateAsync(request, ct);
        return result.Succeeded ? Created($"api/v1/shop/sections/{result.Value!.Id}", result.Value) : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ShopSectionAdminDto>> Update(int id, ShopSectionRequest request, CancellationToken ct) =>
        this.ToActionResult(await sections.UpdateAsync(id, request, ct));

    [HttpPut("order")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Reorder(ShopSectionOrderRequest request, CancellationToken ct) =>
        this.ToNoContent(await sections.ReorderAsync(request.Ids, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToNoContent(await sections.DeleteAsync(id, ct));

    // ---- the card picture: uploaded, or picked from the pictures of the products

    [HttpPut("{id:int}/image")]
    [Authorize(Policy = Policies.AdminOnly)]
    [RejectOversizedUpload]
    [RequestSizeLimit(ImageUpload.RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUpload.FormLimit)]
    public async Task<ActionResult<ShopSectionAdminDto>> SetImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var set = await images.SetShopSectionImageAsync(id, stream, ct);
        return set.Succeeded
            ? this.ToActionResult(await sections.GetAsync(id, ct))
            : this.ToActionResult(set.As<ShopSectionAdminDto>());
    }

    [HttpPut("{id:int}/image/product")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ShopSectionAdminDto>> PickImage(int id, ShopSectionPickedImageRequest request, CancellationToken ct) =>
        this.ToActionResult(await sections.PickImageAsync(id, request.ProductImageId, ct));

    [HttpDelete("{id:int}/image")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ShopSectionAdminDto>> ClearImage(int id, CancellationToken ct)
    {
        var cleared = await images.ClearShopSectionImageAsync(id, ct);
        return cleared.Succeeded
            ? this.ToActionResult(await sections.GetAsync(id, ct))
            : this.ToActionResult(cleared.As<ShopSectionAdminDto>());
    }
}
