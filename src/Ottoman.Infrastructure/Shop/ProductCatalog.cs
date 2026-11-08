using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Images;
using Ottoman.Application.Recommendations;
using Ottoman.Application.Shop;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Images;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Shop;

public class ProductCatalog(OttomanDbContext db, IRecommendationCache recommendations, ImageUrlBuilder images) : IProductCatalog
{
    // ---------------------------------------------------------------- overview

    public async Task<ShopOverviewDto> GetOverviewAsync(CancellationToken ct)
    {
        // Everything on the shelf, reduced to what the tiles need (a few hundred rows at most).
        var rows = await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Stock > 0)
            .OrderByDescending(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.CategoryId,
                p.BrandId,
                p.Price,
                p.SalePrice,
                Cover = p.Images.OrderBy(i => i.SortOrder).Select(i => i.FileKey).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var categories = await db.Categories.AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync(ct);
        var brands = await db.Brands.AsNoTracking().Select(b => new { b.Id, b.Name, b.LogoKey }).ToListAsync(ct);

        // Rows are newest first, so the first one with a picture is the newest product that has one.
        ShopTileDto Tile(int id, string name, IEnumerable<string?> covers, int count) =>
            new(id, name, count, images.Url(covers.FirstOrDefault(k => k is not null)));

        var categoryTiles = categories
            .Select(c =>
            {
                var mine = rows.Where(r => r.CategoryId == c.Id).ToList();
                return Tile(c.Id, c.Name, mine.Select(r => r.Cover), mine.Count);
            })
            .Where(t => t.ProductCount > 0)
            .OrderByDescending(t => t.ProductCount).ThenBy(t => t.Name)
            .ToList();
        var brandTiles = brands
            .Select(b =>
            {
                var mine = rows.Where(r => r.BrandId == b.Id).ToList();
                // A logo the admin uploaded beats a product photo.
                return b.LogoKey is not null
                    ? new ShopTileDto(b.Id, b.Name, mine.Count, images.Url(b.LogoKey), IsLogo: true)
                    : Tile(b.Id, b.Name, mine.Select(r => r.Cover), mine.Count);
            })
            .Where(t => t.ProductCount > 0)
            .OrderByDescending(t => t.ProductCount).ThenBy(t => t.Name)
            .ToList();

        return new ShopOverviewDto(
            categoryTiles,
            brandTiles,
            [],
            []);
    }

    // ---------------------------------------------------------------- products

    public async Task<PagedResult<ProductDto>> ListAsync(ProductQuery q, PageQuery page, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking().AsQueryable();
        if (!q.IncludeInactive) query = query.Where(p => p.IsActive);
        if (q.CategoryId is { } c) query = query.Where(p => p.CategoryId == c);
        if (q.BrandId is { } b) query = query.Where(p => p.BrandId == b);
        if (q.OnlyOnSale) query = query.Where(p => p.SalePrice != null);
        if (q.OnlyLowStock) query = query.Where(p => p.LowStockThreshold != null && p.Stock <= p.LowStockThreshold);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)) ||
                                     (p.Brand != null && p.Brand.Name.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        query = q.Sort switch
        {
            ProductSort.PriceAsc => query.OrderBy(p => p.SalePrice ?? p.Price).ThenBy(p => p.Id),
            ProductSort.PriceDesc => query.OrderByDescending(p => p.SalePrice ?? p.Price).ThenBy(p => p.Id),
            ProductSort.Newest => query.OrderByDescending(p => p.Id),
            _ => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
        };

        var items = await query.Skip(page.Skip).Take(page.SafePageSize)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description,
                p.BrandId, p.Brand != null ? p.Brand.Name : null,
                p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.Price, p.Stock, p.LowStockThreshold,
                p.LowStockThreshold != null && p.Stock <= p.LowStockThreshold,
                p.IsActive, null,
                p.Images.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Id, i.FileKey)).ToList(), p.SalePrice))
            .ToListAsync(ct);
        return new PagedResult<ProductDto>(items.Select(Resolve).ToList(), total);
    }

    public async Task<Result<ProductDto>> GetAsync(int id, bool includeInactive, CancellationToken ct)
    {
        var p = await LoadDtoAsync(id, ct);
        return p is null || (!p.IsActive && !includeInactive) ? Result<ProductDto>.NotFound() : Result<ProductDto>.Ok(p);
    }

    public async Task<IReadOnlyList<ProductDto>> GetManyAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var rows = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsActive)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description,
                p.BrandId, p.Brand != null ? p.Brand.Name : null,
                p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.Price, p.Stock, p.LowStockThreshold,
                p.LowStockThreshold != null && p.Stock <= p.LowStockThreshold,
                p.IsActive, null,
                p.Images.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Id, i.FileKey)).ToList(), p.SalePrice))
            .ToListAsync(ct);
        var byId = rows.Select(Resolve).ToDictionary(p => p.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    public async Task<Result<ProductDto>> CreateAsync(ProductUpsertRequest r, CancellationToken ct)
    {
        var refs = await CheckReferencesAsync(r, ct);
        if (refs is not null) return Result<ProductDto>.Invalid(refs);

        var name = r.Name.Trim();
        if (await db.Products.AnyAsync(p => p.Name == name, ct))
            return Result<ProductDto>.Conflict("Proizvod s tim nazivom već postoji.");

        var product = new Product
        {
            Name = name,
            Description = r.Description?.Trim(),
            BrandId = r.BrandId,
            CategoryId = r.CategoryId,
            Price = r.Price,
            SalePrice = r.SalePrice,
            Stock = r.Stock,
            LowStockThreshold = r.LowStockThreshold,
            IsActive = r.IsActive,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return Result<ProductDto>.Ok((await LoadDtoAsync(product.Id, ct))!);
    }

    public async Task<Result<ProductDto>> UpdateAsync(int id, ProductUpsertRequest r, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null) return Result<ProductDto>.NotFound();

        var refs = await CheckReferencesAsync(r, ct);
        if (refs is not null) return Result<ProductDto>.Invalid(refs);

        var name = r.Name.Trim();
        if (await db.Products.AnyAsync(p => p.Id != id && p.Name == name, ct))
            return Result<ProductDto>.Conflict("Proizvod s tim nazivom već postoji.");

        // Stock is deliberately not touched: concurrent orders change it, so only the atomic adjust may.
        product.Name = name;
        product.Description = r.Description?.Trim();
        product.BrandId = r.BrandId;
        product.CategoryId = r.CategoryId;
        product.Price = r.Price;
        product.SalePrice = r.SalePrice;
        product.LowStockThreshold = r.LowStockThreshold;
        product.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        return Result<ProductDto>.Ok((await LoadDtoAsync(id, ct))!);
    }

    public async Task<Result<ProductDto>> AdjustStockAsync(int id, int delta, CancellationToken ct)
    {
        // One conditional UPDATE: the stock can never go negative, whatever else is running.
        var rows = await db.Products
            .Where(p => p.Id == id && p.Stock + delta >= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + delta), ct);
        if (rows == 1) return Result<ProductDto>.Ok((await LoadDtoAsync(id, ct))!);

        return await db.Products.AnyAsync(p => p.Id == id, ct)
            ? Result<ProductDto>.Conflict("Zaliha ne može biti negativna.")
            : Result<ProductDto>.NotFound();
    }

    public async Task<Result<bool>> DeactivateAsync(int id, CancellationToken ct)
    {
        var rows = await db.Products.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), ct);
        if (rows == 1) recommendations.Invalidate();
        return rows == 1 ? Result<bool>.Ok(true) : Result<bool>.NotFound();
    }

    private async Task<string?> CheckReferencesAsync(ProductUpsertRequest r, CancellationToken ct)
    {
        if (r.BrandId is { } b && !await db.Brands.AnyAsync(x => x.Id == b, ct)) return "Odabrani brend ne postoji.";
        if (r.CategoryId is { } c && !await db.Categories.AnyAsync(x => x.Id == c, ct)) return "Odabrana kategorija ne postoji.";
        return null;
    }

    private async Task<ProductDto?> LoadDtoAsync(int id, CancellationToken ct)
    {
        var dto = await db.Products.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new ProductDto(
                p.Id, p.Name, p.Description,
                p.BrandId, p.Brand != null ? p.Brand.Name : null,
                p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.Price, p.Stock, p.LowStockThreshold,
                p.LowStockThreshold != null && p.Stock <= p.LowStockThreshold,
                p.IsActive, null,
                p.Images.OrderBy(i => i.SortOrder).Select(i => new ProductImageDto(i.Id, i.FileKey)).ToList(), p.SalePrice))
            .FirstOrDefaultAsync(ct);
        return dto is null ? null : Resolve(dto);
    }

    /// <summary>The queries carry storage keys; this turns them into delivery URLs and picks the first as the cover.</summary>
    private ProductDto Resolve(ProductDto p)
    {
        var pictures = p.Images.Select(i => new ProductImageDto(i.Id, images.Url(i.Url) ?? "")).Where(i => i.Url != "").ToList();
        return p with { Images = pictures, ImageUrl = pictures.FirstOrDefault()?.Url };
    }

    // ---------------------------------------------------------------- brands

    public async Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken ct) =>
        (await db.Brands.AsNoTracking().OrderBy(b => b.Name).Select(b => new { b.Id, b.Name, b.LogoKey }).ToListAsync(ct))
            .Select(b => new BrandDto(b.Id, b.Name, images.Url(b.LogoKey))).ToList();

    public async Task<PagedResult<BrandDto>> PageBrandsAsync(string? search, PageQuery page, CancellationToken ct)
    {
        var query = db.Brands.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(b => b.Name.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(b => b.Name).ThenBy(b => b.Id).Skip(page.Skip).Take(page.SafePageSize)
            .Select(b => new { b.Id, b.Name, b.LogoKey }).ToListAsync(ct);
        return new PagedResult<BrandDto>(items.Select(b => new BrandDto(b.Id, b.Name, images.Url(b.LogoKey))).ToList(), total);
    }

    public async Task<PagedResult<CategoryDto>> PageCategoriesAsync(string? search, PageQuery page, CancellationToken ct)
    {
        var query = db.Categories.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Description != null && c.Description.Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip(page.Skip).Take(page.SafePageSize)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description)).ToListAsync(ct);
        return new PagedResult<CategoryDto>(items, total);
    }

    public async Task<Result<BrandDto>> GetBrandAsync(int id, CancellationToken ct)
    {
        var b = await db.Brands.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.Name, x.LogoKey }).FirstOrDefaultAsync(ct);
        return b is null ? Result<BrandDto>.NotFound() : Result<BrandDto>.Ok(new BrandDto(b.Id, b.Name, images.Url(b.LogoKey)));
    }

    public async Task<Result<BrandDto>> CreateBrandAsync(NameRequest r, CancellationToken ct)
    {
        var name = r.Name.Trim();
        if (await db.Brands.AnyAsync(b => b.Name == name, ct)) return Result<BrandDto>.Conflict("Brend s tim nazivom već postoji.");
        var brand = new Brand { Name = name };
        db.Brands.Add(brand);
        await db.SaveChangesAsync(ct);
        return Result<BrandDto>.Ok(new BrandDto(brand.Id, brand.Name, images.Url(brand.LogoKey)));
    }

    public async Task<Result<BrandDto>> UpdateBrandAsync(int id, NameRequest r, CancellationToken ct)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null) return Result<BrandDto>.NotFound();
        var name = r.Name.Trim();
        if (await db.Brands.AnyAsync(b => b.Id != id && b.Name == name, ct)) return Result<BrandDto>.Conflict("Brend s tim nazivom već postoji.");
        brand.Name = name;
        await db.SaveChangesAsync(ct);
        return Result<BrandDto>.Ok(new BrandDto(brand.Id, brand.Name, images.Url(brand.LogoKey)));
    }

    public async Task<Result<bool>> DeleteBrandAsync(int id, CancellationToken ct)
    {
        // One statement: the brand goes only while nothing points at it, so products never lose their brand silently
        // (the FK would SET NULL) and no shop section is left filtering by a brand that no longer exists.
        var rows = await db.Brands
            .Where(b => b.Id == id && !db.Products.Any(p => p.BrandId == id) && !db.ShopSections.Any(BrandUse(id)))
            .ExecuteDeleteAsync(ct);
        if (rows == 1)
        {
            recommendations.Invalidate();
            return Result<bool>.Ok(true);
        }
        if (!await db.Brands.AnyAsync(b => b.Id == id, ct)) return Result<bool>.NotFound();
        var products = await db.Products.CountAsync(p => p.BrandId == id, ct);
        var sections = await db.ShopSections.CountAsync(BrandUse(id), ct);
        return Result<bool>.Conflict(CatalogUsage.InUseMessage(isBrand: true, products, sections));
    }

    /// <summary>A section that filters by the brand or shows it as a hand-picked brand tile.</summary>
    private static System.Linq.Expressions.Expression<Func<ShopSection, bool>> BrandUse(int id) =>
        s => s.BrandId == id || (s.Kind == ShopSectionKind.BrandTiles && s.Entries.Any(e => e.EntityId == id));

    /// <summary>A section that lists the category (deleting it would delete the section) or shows it as a hand-picked tile.</summary>
    private static System.Linq.Expressions.Expression<Func<ShopSection, bool>> CategoryUse(int id) =>
        s => s.CategoryId == id || (s.Kind == ShopSectionKind.CategoryTiles && s.Entries.Any(e => e.EntityId == id));

    // ---------------------------------------------------------------- categories

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new CategoryDto(c.Id, c.Name, c.Description)).ToListAsync(ct);

    public async Task<Result<CategoryDto>> CreateCategoryAsync(NameRequest r, CancellationToken ct)
    {
        var name = r.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name, ct)) return Result<CategoryDto>.Conflict("Kategorija s tim nazivom već postoji.");
        var category = new Category { Name = name, Description = r.Description?.Trim() };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return Result<CategoryDto>.Ok(new CategoryDto(category.Id, category.Name, category.Description));
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(int id, NameRequest r, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return Result<CategoryDto>.NotFound();
        var name = r.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Name == name, ct)) return Result<CategoryDto>.Conflict("Kategorija s tim nazivom već postoji.");
        category.Name = name;
        category.Description = r.Description?.Trim();
        await db.SaveChangesAsync(ct);
        return Result<CategoryDto>.Ok(new CategoryDto(category.Id, category.Name, category.Description));
    }

    public async Task<Result<bool>> DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var rows = await db.Categories
            .Where(c => c.Id == id && !db.Products.Any(p => p.CategoryId == id) && !db.ShopSections.Any(CategoryUse(id)))
            .ExecuteDeleteAsync(ct);
        if (rows == 1)
        {
            recommendations.Invalidate();
            return Result<bool>.Ok(true);
        }
        if (!await db.Categories.AnyAsync(c => c.Id == id, ct)) return Result<bool>.NotFound();
        var products = await db.Products.CountAsync(p => p.CategoryId == id, ct);
        var sections = await db.ShopSections.CountAsync(CategoryUse(id), ct);
        return Result<bool>.Conflict(CatalogUsage.InUseMessage(isBrand: false, products, sections));
    }
}
