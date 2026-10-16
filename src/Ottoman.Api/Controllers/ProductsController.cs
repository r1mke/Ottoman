using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application.Common;
using Ottoman.Application.Images;
using Ottoman.Application.Shop;
using Ottoman.Domain;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductsController(IProductCatalog catalog, IImageAttachments images) : ControllerBase
{
    /// <summary>Catalogue with search, filters, sorting and paging. Inactive products and low-stock filtering are admin-only.</summary>
    [HttpGet]
    public Task<PagedResult<ProductDto>> List(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] int? brandId,
        [FromQuery] ProductSort sort = ProductSort.Name,
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool lowStock = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var admin = User.IsInRole(Roles.Admin);
        return catalog.ListAsync(
            new ProductQuery(search, categoryId, brandId, sort, includeInactive && admin, lowStock && admin),
            new PageQuery(page, pageSize), ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> Get(int id, CancellationToken ct) =>
        this.ToActionResult(await catalog.GetAsync(id, User.IsInRole(Roles.Admin), ct));

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ProductDto>> Create(ProductUpsertRequest request, CancellationToken ct)
    {
        var result = await catalog.CreateAsync(request, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ProductDto>> Update(int id, ProductUpsertRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.UpdateAsync(id, request, ct));

    /// <summary>Restock or write off. A single conditional update: the stock never goes below zero.</summary>
    [HttpPost("{id:int}/stock")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ProductDto>> AdjustStock(int id, StockAdjustRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.AdjustStockAsync(id, request.Delta, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct) =>
        this.ToNoContent(await catalog.DeactivateAsync(id, ct));

    [HttpPost("{id:int}/images")]
    [Authorize(Policy = Policies.AdminOnly)]
    [RejectOversizedUpload]
    [RequestSizeLimit(ImageUpload.RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUpload.FormLimit)]
    public async Task<ActionResult<ProductDto>> AddImage(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var added = await images.AddProductImageAsync(id, stream, ct);
        return this.ToActionResult(added.Succeeded ? await catalog.GetAsync(id, true, ct) : added.As<ProductDto>());
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ProductDto>> RemoveImage(int id, int imageId, CancellationToken ct)
    {
        var removed = await images.RemoveProductImageAsync(id, imageId, ct);
        return this.ToActionResult(removed.Succeeded ? await catalog.GetAsync(id, true, ct) : removed.As<ProductDto>());
    }
}

[ApiController]
[Route("api/v1/brands")]
[Authorize]
public class BrandsController(IProductCatalog catalog) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<BrandDto>> List(CancellationToken ct) => catalog.ListBrandsAsync(ct);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<BrandDto>> Create(NameRequest request, CancellationToken ct)
    {
        var result = await catalog.CreateBrandAsync(request, ct);
        return result.Succeeded ? Created($"api/v1/brands/{result.Value!.Id}", result.Value) : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<BrandDto>> Update(int id, NameRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.UpdateBrandAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToNoContent(await catalog.DeleteBrandAsync(id, ct));
}

[ApiController]
[Route("api/v1/categories")]
[Authorize]
public class CategoriesController(IProductCatalog catalog) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> List(CancellationToken ct) => catalog.ListCategoriesAsync(ct);

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<CategoryDto>> Create(NameRequest request, CancellationToken ct)
    {
        var result = await catalog.CreateCategoryAsync(request, ct);
        return result.Succeeded ? Created($"api/v1/categories/{result.Value!.Id}", result.Value) : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<CategoryDto>> Update(int id, NameRequest request, CancellationToken ct) =>
        this.ToActionResult(await catalog.UpdateCategoryAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToNoContent(await catalog.DeleteCategoryAsync(id, ct));
}
