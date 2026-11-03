using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Images;
using Ottoman.Application.Shop;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Images;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Shop;

public class ShopSectionService(
    OttomanDbContext db, IProductCatalog catalog, ImageUrlBuilder images, IImageAttachments attachments) : IShopSectionService
{
    /// <summary>How many products a card opens at most.</summary>
    private const int CardListLimit = 100;

    /// <summary>
    /// What is on the shelf (active and in stock), reduced to what the section rules look at. Read once per request:
    /// every section is then resolved from it, so the number of sections never multiplies the number of queries.
    /// </summary>
    private sealed record ShelfRow(int Id, int? CategoryId, int? BrandId, decimal Price, decimal? SalePrice);

    private async Task<List<ShelfRow>> ShelfAsync(CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(p => p.IsActive && p.Stock > 0)
            .Select(p => new ShelfRow(p.Id, p.CategoryId, p.BrandId, p.Price, p.SalePrice)).ToListAsync(ct);

    private static ShopSectionStyle StyleOf(ShopSection s) =>
        new(s.Background, s.TileSize, s.CardSpan, s.HeadingAlign, s.ShowSeeAll);

    private IQueryable<ShopSection> Full() => db.ShopSections.AsNoTracking()
        .Include(s => s.Products).Include(s => s.Entries).Include(s => s.Category).Include(s => s.Brand);

    // ------------------------------------------------------------------ what customers see

    public async Task<IReadOnlyList<ShopBlockDto>> GetBlocksAsync(
        IReadOnlyList<ShopTileDto> categoryTiles, IReadOnlyList<ShopTileDto> brandTiles, CancellationToken ct)
    {
        var sections = await Full().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct);
        var pickedUrls = await PickedUrlsAsync(sections, ct);
        var shelf = await ShelfAsync(ct);

        // Which products each product section shows, and then all of those products loaded in one go.
        var idsBySection = sections.Where(s => !ShopSectionKinds.IsBuiltIn(s.Kind))
            .ToDictionary(s => s.Id, s => ResolveIds(s, s.MaxItems, shelf));
        var products = await ProductsByIdAsync(idsBySection.Values.SelectMany(x => x), ct);

        var blocks = new List<ShopBlockDto>();
        foreach (var s in sections)
        {
            switch (s.Kind)
            {
                case ShopSectionKind.CategoryTiles:
                case ShopSectionKind.BrandTiles:
                {
                    var tiles = PickTiles(s, s.Kind == ShopSectionKind.CategoryTiles ? categoryTiles : brandTiles);
                    if (tiles.Count > 0)
                        blocks.Add(new ShopBlockDto(s.Id, s.Kind, s.Title, s.Eyebrow, ShopSectionLayout.Row, null, null, tiles, StyleOf(s)));
                    break;
                }
                case ShopSectionKind.Recommended:
                    blocks.Add(new ShopBlockDto(s.Id, s.Kind, s.Title, s.Eyebrow, ShopSectionLayout.Row, null, null, null, StyleOf(s)));
                    break;
                default:
                {
                    var own = idsBySection[s.Id].Where(products.ContainsKey).Select(id => products[id]).ToList();
                    if (own.Count == 0) break; // nothing to show: customers do not see the section
                    // Only a banner shows a picture of its own: the admin's, or else the first product photo.
                    var banner = s.Layout == ShopSectionLayout.Banner
                        ? images.Url(s.ImageKey) ?? pickedUrls.GetValueOrDefault(s.Id)
                          ?? own.Select(p => p.ImageUrl).FirstOrDefault(u => u is not null)
                        : null;
                    blocks.Add(new ShopBlockDto(s.Id, s.Kind, s.Title, s.Eyebrow, s.Layout, banner, own, null, StyleOf(s)));
                    break;
                }
            }
        }
        return blocks;
    }

    /// <summary>
    /// The tiles a tile block shows: the admin's picks in their order, or else the busiest ones. Tiles without products
    /// are never there (the lists passed in only hold those that have). Categories always come out in an even number,
    /// so the last row of the two-column grid is full.
    /// </summary>
    private static List<ShopTileDto> PickTiles(ShopSection s, IReadOnlyList<ShopTileDto> all)
    {
        var tiles = s.Entries.Count > 0
            ? s.Entries.OrderBy(e => e.SortOrder).Select(e => all.FirstOrDefault(t => t.Id == e.EntityId)).OfType<ShopTileDto>().ToList()
            : all.Take(s.MaxItems).ToList();
        if (s.Kind == ShopSectionKind.CategoryTiles && tiles.Count % 2 == 1) tiles.RemoveAt(tiles.Count - 1);
        return tiles;
    }

    public async Task<IReadOnlyList<ShopCardDto>> GetCardsAsync(CancellationToken ct)
    {
        var sections = await Full().Where(s => s.IsActive && s.ShowAsCard
                         // The method cannot be translated to SQL, so the built-in kinds are named here.
                         && s.Kind != ShopSectionKind.CategoryTiles && s.Kind != ShopSectionKind.BrandTiles
                         && s.Kind != ShopSectionKind.Recommended)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct);
        if (sections.Count == 0) return [];
        var pickedUrls = await PickedUrlsAsync(sections, ct);
        var shelf = await ShelfAsync(ct);
        var idsBySection = sections.ToDictionary(s => s.Id, s => ResolveIds(s, CardListLimit, shelf));
        var products = await ProductsByIdAsync(idsBySection.Values.SelectMany(x => x), ct);

        var cards = new List<ShopCardDto>();
        foreach (var s in sections)
        {
            var own = idsBySection[s.Id].Where(products.ContainsKey).Select(id => products[id]).ToList();
            if (own.Count == 0) continue;

            var deepest = own.Where(p => p.SalePrice is not null)
                .Select(p => (int)Math.Round((1 - p.SalePrice!.Value / p.Price) * 100)).DefaultIfEmpty(0).Max();
            // The admin's picture wins; otherwise the first product that has a picture of its own.
            var picture = images.Url(s.ImageKey)
                          ?? pickedUrls.GetValueOrDefault(s.Id)
                          ?? own.Select(p => p.ImageUrl).FirstOrDefault(u => u is not null);
            cards.Add(new ShopCardDto(s.Id, s.Title, s.Kind, own.Count, deepest, picture, s.Eyebrow, StyleOf(s)));
        }
        return cards;
    }

    public async Task<Result<PagedResult<ProductDto>>> GetProductsAsync(int id, PageQuery page, CancellationToken ct)
    {
        var section = await Full().FirstOrDefaultAsync(s => s.Id == id && s.IsActive, ct);
        if (section is null || ShopSectionKinds.IsBuiltIn(section.Kind)) return Result<PagedResult<ProductDto>>.NotFound();

        var ids = ResolveIds(section, CardListLimit, await ShelfAsync(ct));
        var shown = await catalog.GetManyAsync(ids, ct);
        return Result<PagedResult<ProductDto>>.Ok(
            new PagedResult<ProductDto>(shown.Skip(page.Skip).Take(page.SafePageSize).ToList(), shown.Count));
    }

    /// <summary>All the products of the given ids, in one query, by id.</summary>
    private async Task<Dictionary<int, ProductDto>> ProductsByIdAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return [];
        return (await catalog.GetManyAsync(wanted, ct)).ToDictionary(p => p.Id);
    }

    /// <summary>The ids of the products a section shows, resolved from the shelf (no query).</summary>
    private static List<int> ResolveIds(ShopSection s, int limit, IReadOnlyList<ShelfRow> shelf) => s.Kind switch
    {
        ShopSectionKind.NewArrivals => shelf.OrderByDescending(p => p.Id).Select(p => p.Id).Take(limit).ToList(),
        ShopSectionKind.OnSale => shelf.Where(p => p.SalePrice != null)
            .OrderByDescending(p => (p.Price - p.SalePrice!.Value) / p.Price).ThenBy(p => p.Id)
            .Select(p => p.Id).Take(limit).ToList(),
        // A section whose category and brand were both deleted has no filter left: show nothing, not everything.
        ShopSectionKind.Category when s.CategoryId is null && s.BrandId is null => [],
        ShopSectionKind.Category => shelf
            .Where(p => (s.CategoryId == null || p.CategoryId == s.CategoryId) && (s.BrandId == null || p.BrandId == s.BrandId))
            .OrderByDescending(p => p.Id).Select(p => p.Id).Take(limit).ToList(),
        ShopSectionKind.Manual => s.Products.OrderBy(p => p.SortOrder).Select(p => p.ProductId).Take(limit).ToList(),
        _ => [],
    };

    // ------------------------------------------------------------------ what the admin sees and edits

    public async Task<PagedResult<ShopSectionAdminDto>> ListForAdminAsync(PageQuery page, CancellationToken ct)
    {
        var total = await db.ShopSections.CountAsync(ct);
        var rows = await Full().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        var picked = await PickedUrlsAsync(rows, ct);

        // What each one shows right now: an empty section is hidden from customers, and the admin should know.
        var shelf = await ShelfAsync(ct);
        ShopOverviewDto? tiles = rows.Any(s => ShopSectionKinds.IsBuiltIn(s.Kind)) ? await catalog.GetOverviewAsync(ct) : null;
        var items = rows.Select(s => ToAdminDto(s, picked.GetValueOrDefault(s.Id), CountShown(s, tiles, shelf))).ToList();
        return new PagedResult<ShopSectionAdminDto>(items, total);
    }

    private static int CountShown(ShopSection s, ShopOverviewDto? tiles, IReadOnlyList<ShelfRow> shelf) => s.Kind switch
    {
        ShopSectionKind.CategoryTiles => PickTiles(s, tiles?.Categories ?? []).Count,
        ShopSectionKind.BrandTiles => PickTiles(s, tiles?.Brands ?? []).Count,
        ShopSectionKind.Recommended => 1,
        _ => ResolveIds(s, CardListLimit, shelf).Count,
    };

    public async Task<Result<ShopSectionAdminDto>> GetAsync(int id, CancellationToken ct)
    {
        var exists = await db.ShopSections.AnyAsync(s => s.Id == id, ct);
        return exists ? Result<ShopSectionAdminDto>.Ok(await LoadAsync(id, ct)) : Result<ShopSectionAdminDto>.NotFound();
    }

    public async Task<Result<ShopSectionAdminDto>> CreateAsync(ShopSectionRequest r, CancellationToken ct)
    {
        var error = await CheckReferencesAsync(r, null, ct);
        if (error is not null) return error;

        var next = (await db.ShopSections.MaxAsync(s => (int?)s.SortOrder, ct) ?? -1) + 1;
        var section = new ShopSection { SortOrder = next };
        Apply(section, r);
        db.ShopSections.Add(section);
        await db.SaveChangesAsync(ct);
        return Result<ShopSectionAdminDto>.Ok(await LoadAsync(section.Id, ct));
    }

    public async Task<Result<ShopSectionAdminDto>> UpdateAsync(int id, ShopSectionRequest r, CancellationToken ct)
    {
        var section = await db.ShopSections.Include(s => s.Products).Include(s => s.Entries).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (section is null) return Result<ShopSectionAdminDto>.NotFound();
        var error = await CheckReferencesAsync(r, id, ct);
        if (error is not null) return error;

        Apply(section, r);
        await db.SaveChangesAsync(ct);
        return Result<ShopSectionAdminDto>.Ok(await LoadAsync(id, ct));
    }

    public async Task<Result<ShopSectionAdminDto>> PickImageAsync(int id, int productImageId, CancellationToken ct)
    {
        if (!await db.ProductImages.AnyAsync(i => i.Id == productImageId, ct))
            return Result<ShopSectionAdminDto>.Invalid("Odabrana slika proizvoda ne postoji.");

        // Drops an uploaded picture (and deletes it from the store) before the pick is recorded.
        var cleared = await attachments.ClearShopSectionImageAsync(id, ct);
        if (!cleared.Succeeded) return new Result<ShopSectionAdminDto>(cleared.Status, default, cleared.Error);

        await db.ShopSections.Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ImageProductImageId, productImageId), ct);
        return Result<ShopSectionAdminDto>.Ok(await LoadAsync(id, ct));
    }

    public async Task<Result<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var kind = await db.ShopSections.Where(s => s.Id == id).Select(s => (ShopSectionKind?)s.Kind).FirstOrDefaultAsync(ct);
        if (kind is null) return Result<bool>.NotFound();
        // The category, brand and "you may like" blocks are hidden, not deleted: they would only come back with the next start.
        if (ShopSectionKinds.IsBuiltIn(kind.Value))
            return Result<bool>.Conflict("Ovaj blok se ne briše. Isključite ga prekidačem „Prikazano kupcima“.");

        // The card picture goes with the section.
        await attachments.ClearShopSectionImageAsync(id, ct);
        var rows = await db.ShopSections.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return rows == 1 ? Result<bool>.Ok(true) : Result<bool>.NotFound();
    }

    public async Task<Result<bool>> ReorderAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var sections = await db.ShopSections.ToListAsync(ct);
        if (ids.Count != sections.Count || ids.Distinct().Count() != ids.Count || sections.Any(s => !ids.Contains(s.Id)))
            return Result<bool>.Invalid("Redoslijed mora sadržavati sve sekcije tačno jednom.");
        for (var i = 0; i < ids.Count; i++) sections.Single(s => s.Id == ids[i]).SortOrder = i;
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    /// <summary>Null when the request is fine, otherwise the failure to return.</summary>
    private async Task<Result<ShopSectionAdminDto>?> CheckReferencesAsync(ShopSectionRequest r, int? existingId, CancellationToken ct)
    {
        if (ShopSectionKinds.IsBuiltIn(r.Kind)
            && await db.ShopSections.AnyAsync(s => s.Kind == r.Kind && s.Id != existingId, ct))
            return Result<ShopSectionAdminDto>.Conflict("Taj blok već postoji. Uredite postojeći.");

        if (r.Kind == ShopSectionKind.Category)
        {
            if (r.CategoryId is { } c && !await db.Categories.AnyAsync(x => x.Id == c, ct))
                return Result<ShopSectionAdminDto>.Invalid("Odabrana kategorija ne postoji.");
            if (r.BrandId is { } b && !await db.Brands.AnyAsync(x => x.Id == b, ct))
                return Result<ShopSectionAdminDto>.Invalid("Odabrani brend ne postoji.");
        }
        if (r.Kind == ShopSectionKind.Manual)
        {
            var ids = r.ProductIds!.Distinct().ToList();
            if (await db.Products.CountAsync(p => ids.Contains(p.Id), ct) != ids.Count)
                return Result<ShopSectionAdminDto>.Invalid("Neki od odabranih proizvoda ne postoji.");
        }
        if (r.Kind is ShopSectionKind.CategoryTiles or ShopSectionKind.BrandTiles && r.EntryIds is { Count: > 0 } picks)
        {
            var wanted = picks.Distinct().ToList();
            var known = r.Kind == ShopSectionKind.CategoryTiles
                ? await db.Categories.CountAsync(x => wanted.Contains(x.Id), ct)
                : await db.Brands.CountAsync(x => wanted.Contains(x.Id), ct);
            if (known != wanted.Count) return Result<ShopSectionAdminDto>.Invalid("Neka od odabranih stavki ne postoji.");
        }
        return null;
    }

    private static void Apply(ShopSection s, ShopSectionRequest r)
    {
        var builtIn = ShopSectionKinds.IsBuiltIn(r.Kind);
        s.Title = ShopSectionTitle.Normalize(r.Title);
        var eyebrow = ShopSectionTitle.Normalize(r.Eyebrow);
        s.Eyebrow = eyebrow.Length == 0 ? null : eyebrow;
        s.Kind = r.Kind;
        s.CategoryId = r.Kind == ShopSectionKind.Category ? r.CategoryId : null;
        s.BrandId = r.Kind == ShopSectionKind.Category ? r.BrandId : null;
        s.MaxItems = r.MaxItems;
        s.IsActive = r.IsActive;
        // The built-in blocks have no card, no picture and no layout of their own.
        s.ShowAsCard = !builtIn && r.ShowAsCard;
        s.Layout = builtIn ? ShopSectionLayout.Row : r.Layout;

        // Look: a missing style means the defaults. Cards and "see all" only make sense for a list of products.
        var style = r.Style ?? new ShopSectionStyle();
        s.Background = style.Background;
        s.TileSize = builtIn ? ShopTileSize.Normal : style.TileSize;
        s.CardSpan = builtIn ? ShopCardSpan.Half : style.CardSpan;
        s.HeadingAlign = style.HeadingAlign;
        s.ShowSeeAll = !builtIn && style.ShowSeeAll;

        s.Products.Clear();
        if (r.Kind == ShopSectionKind.Manual)
        {
            var order = 0;
            foreach (var id in r.ProductIds!.Distinct())
                s.Products.Add(new ShopSectionProduct { ProductId = id, SortOrder = order++ });
        }
        s.Entries.Clear();
        if (r.Kind is ShopSectionKind.CategoryTiles or ShopSectionKind.BrandTiles && r.EntryIds is { Count: > 0 })
        {
            var order = 0;
            foreach (var id in r.EntryIds.Distinct())
                s.Entries.Add(new ShopSectionEntry { EntityId = id, SortOrder = order++ });
        }
    }

    /// <summary>The delivery URLs of the product pictures the admin picked, by section id (a deleted picture is skipped).</summary>
    private async Task<Dictionary<int, string>> PickedUrlsAsync(IEnumerable<ShopSection> sections, CancellationToken ct)
    {
        var picks = sections.Where(s => s.ImageProductImageId != null).ToDictionary(s => s.Id, s => s.ImageProductImageId!.Value);
        if (picks.Count == 0) return [];
        var wanted = picks.Values.Distinct().ToList();
        var keys = await db.ProductImages.AsNoTracking().Where(i => wanted.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.FileKey, ct);
        var result = new Dictionary<int, string>();
        foreach (var (sectionId, imageId) in picks)
        {
            if (keys.TryGetValue(imageId, out var key) && images.Url(key) is { } url) result[sectionId] = url;
        }
        return result;
    }

    private async Task<ShopSectionAdminDto> LoadAsync(int id, CancellationToken ct)
    {
        var s = await Full().SingleAsync(x => x.Id == id, ct);
        var picked = await PickedUrlsAsync([s], ct);
        var tiles = ShopSectionKinds.IsBuiltIn(s.Kind) ? await catalog.GetOverviewAsync(ct) : null;
        return ToAdminDto(s, picked.GetValueOrDefault(s.Id), CountShown(s, tiles, await ShelfAsync(ct)));
    }

    private ShopSectionAdminDto ToAdminDto(ShopSection s, string? pickedUrl, int shown) => new(
        s.Id, s.Title, s.Kind, s.CategoryId, s.Category?.Name, s.BrandId, s.Brand?.Name, s.MaxItems, s.SortOrder, s.IsActive,
        s.Products.OrderBy(p => p.SortOrder).Select(p => p.ProductId).ToList(),
        s.ShowAsCard,
        images.Url(s.ImageKey) ?? pickedUrl,
        pickedUrl is null ? null : s.ImageProductImageId,
        s.Layout,
        s.Eyebrow,
        shown,
        s.Entries.OrderBy(e => e.SortOrder).Select(e => e.EntityId).ToList(),
        StyleOf(s));
}
