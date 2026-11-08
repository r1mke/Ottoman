using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Seeding;

/// <summary>
/// The salon's real catalog (categories, brands, products with their pictures, services, gallery, salon profile, shop
/// front page) as it was exported from a working database. Picture keys point at files that already exist on the
/// author's Cloudinary account: nothing is uploaded, the keys are only stored, so the pictures show as soon as that
/// cloud name is configured and the apps show empty placeholders without it. The ids inside the file are only relation
/// keys between its own lists; real ids come from the database.
/// </summary>
public sealed class SeedCatalog
{
    public const string ResourceName = "Ottoman.Infrastructure.Seeding.catalog.json";

    public List<TitleRow> EmployeeTitles { get; init; } = [];
    public List<CategoryRow> Categories { get; init; } = [];
    public List<BrandRow> Brands { get; init; } = [];
    public List<ProductRow> Products { get; init; } = [];
    public List<ProductImageRow> ProductImages { get; init; } = [];
    public List<ServiceRow> Services { get; init; } = [];
    public List<HaircutRow> HaircutCategories { get; init; } = [];
    public List<GalleryRow> Gallery { get; init; } = [];
    public List<AnnouncementRow> Announcements { get; init; } = [];
    public List<SalonRow> Salon { get; init; } = [];
    public List<SectionRow> ShopSections { get; init; } = [];
    public List<SectionEntryRow> ShopSectionEntries { get; init; } = [];
    public List<StaffPhotoRow> DemoStaffPhotos { get; init; } = [];

    public sealed record TitleRow(int Id, string Name);
    public sealed record CategoryRow(int Id, string Name, string? Description);
    public sealed record BrandRow(int Id, string Name, string? LogoKey);
    public sealed record ProductRow(
        int Id, string Name, string? Description, int? BrandId, int? CategoryId, decimal Price, decimal? SalePrice,
        int Stock, int? LowStockThreshold, bool IsActive);
    public sealed record ProductImageRow(int ProductId, string FileKey, int SortOrder, string? ContentHash);
    public sealed record ServiceRow(int Id, string Name, string? Description, decimal Price, int DurationMinutes, string? ImageKey, bool IsActive);
    public sealed record HaircutRow(int Id, string Name, int SortOrder);
    public sealed record GalleryRow(int Id, string ImageKey, string? ContentHash, int CategoryId, string? Caption, bool IsFeatured);
    public sealed record AnnouncementRow(string Title, string Body, string Audience, string? ImageKey);
    public sealed record SalonRow(
        string Name, string? Address, string? Phone, string? Description, string? LogoKey, string? MapsUrl,
        double? Latitude, double? Longitude, string? FacebookUrl, string? InstagramUrl);
    public sealed record SectionRow(
        int Id, string Title, string Kind, int? CategoryId, int MaxItems, int SortOrder, bool IsActive, int? BrandId,
        string? ImageKey, int? ImageProductImageId, bool ShowAsCard, string Layout, string? Eyebrow, string Background,
        string CardSpan, string HeadingAlign, bool ShowSeeAll, string TileSize);
    public sealed record SectionEntryRow(int ShopSectionId, int EntityId, int SortOrder);
    public sealed record StaffPhotoRow(string Email, string PhotoKey);

    private static readonly Lazy<SeedCatalog> Cached = new(Read);

    public static SeedCatalog Load() => Cached.Value;

    private static SeedCatalog Read()
    {
        using var stream = typeof(SeedCatalog).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        return JsonSerializer.Deserialize<SeedCatalog>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException("The demo catalog is empty.");
    }
}

/// <summary>
/// Loads <see cref="SeedCatalog"/> into the database. Idempotent: every group is matched by its natural key (name,
/// picture key) and only what is missing is added, one SaveChanges per group, so it is safe on every start and on a
/// database that an older version of the demo seed already filled. Nothing an admin has edited is overwritten.
/// </summary>
public sealed class CatalogSeeder(OttomanDbContext db, ILogger<CatalogSeeder> log)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var c = SeedCatalog.Load();

        await SeedTitlesAsync(c, ct);
        var categories = await SeedCategoriesAsync(c, ct);
        var brands = await SeedBrandsAsync(c, ct);
        await SeedProductsAsync(c, categories, brands, ct);
        await SeedServicesAsync(c, ct);
        var haircuts = await SeedHaircutCategoriesAsync(c, ct);
        await SeedGalleryAsync(c, haircuts, ct);
        await SeedSalonAsync(c, ct);
        await SeedShopSectionsAsync(c, categories, brands, ct);
    }

    private async Task SeedTitlesAsync(SeedCatalog c, CancellationToken ct)
    {
        var have = (await db.EmployeeTitles.Select(t => t.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var t in c.EmployeeTitles.Where(t => have.Add(t.Name)))
            db.EmployeeTitles.Add(new EmployeeTitle { Name = t.Name });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Old id in the file to the id in this database, by name.</summary>
    private async Task<Dictionary<int, int>> SeedCategoriesAsync(SeedCatalog c, CancellationToken ct)
    {
        var existing = await db.Categories.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var row in c.Categories.Where(r => !existing.ContainsKey(r.Name)))
            db.Categories.Add(existing[row.Name] = new Category { Name = row.Name, Description = row.Description });
        await db.SaveChangesAsync(ct);
        return c.Categories.ToDictionary(r => r.Id, r => existing[r.Name].Id);
    }

    private async Task<Dictionary<int, int>> SeedBrandsAsync(SeedCatalog c, CancellationToken ct)
    {
        var existing = await db.Brands.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var row in c.Brands)
        {
            if (!existing.TryGetValue(row.Name, out var brand))
                db.Brands.Add(existing[row.Name] = new Brand { Name = row.Name, LogoKey = row.LogoKey });
            else if (brand.LogoKey is null && row.LogoKey is not null)
                brand.LogoKey = row.LogoKey;
        }
        await db.SaveChangesAsync(ct);
        return c.Brands.ToDictionary(r => r.Id, r => existing[r.Name].Id);
    }

    private async Task SeedProductsAsync(
        SeedCatalog c, Dictionary<int, int> categories, Dictionary<int, int> brands, CancellationToken ct)
    {
        var existing = await db.Products.ToDictionaryAsync(p => p.Name, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var row in c.Products.Where(r => !existing.ContainsKey(r.Name)))
        {
            db.Products.Add(existing[row.Name] = new Product
            {
                Name = row.Name,
                Description = row.Description,
                BrandId = row.BrandId is { } b ? brands[b] : null,
                CategoryId = row.CategoryId is { } k ? categories[k] : null,
                Price = row.Price,
                SalePrice = row.SalePrice,
                Stock = row.Stock,
                LowStockThreshold = row.LowStockThreshold,
                IsActive = row.IsActive,
            });
        }
        await db.SaveChangesAsync(ct);

        // Pictures only for products that have none yet (new ones, or an older demo product), keys stored as they are.
        var withPictures = (await db.ProductImages.Select(i => i.ProductId).Distinct().ToListAsync(ct)).ToHashSet();
        var nameOf = c.Products.ToDictionary(p => p.Id, p => p.Name);
        var added = 0;
        foreach (var image in c.ProductImages)
        {
            var product = existing[nameOf[image.ProductId]];
            if (withPictures.Contains(product.Id)) continue;
            db.ProductImages.Add(new ProductImage
            {
                ProductId = product.Id,
                FileKey = image.FileKey,
                SortOrder = image.SortOrder,
                ContentHash = image.ContentHash,
            });
            added++;
        }
        await db.SaveChangesAsync(ct);
        if (added > 0) log.LogInformation("Seeded {Count} product picture keys", added);
    }

    private async Task SeedServicesAsync(SeedCatalog c, CancellationToken ct)
    {
        var existing = await db.Services.ToDictionaryAsync(s => s.Name, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var row in c.Services)
        {
            if (!existing.TryGetValue(row.Name, out var service))
                db.Services.Add(existing[row.Name] = new Service
                {
                    Name = row.Name,
                    Description = row.Description,
                    Price = row.Price,
                    DurationMinutes = row.DurationMinutes,
                    ImageKey = row.ImageKey,
                    IsActive = row.IsActive,
                });
            else if (service.ImageKey is null && row.ImageKey is not null)
                service.ImageKey = row.ImageKey;
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<int, int>> SeedHaircutCategoriesAsync(SeedCatalog c, CancellationToken ct)
    {
        var existing = await db.HaircutCategories.ToDictionaryAsync(h => h.Name, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var row in c.HaircutCategories.Where(r => !existing.ContainsKey(r.Name)))
            db.HaircutCategories.Add(existing[row.Name] = new HaircutCategory { Name = row.Name, SortOrder = row.SortOrder });
        await db.SaveChangesAsync(ct);
        return c.HaircutCategories.ToDictionary(r => r.Id, r => existing[r.Name].Id);
    }

    private async Task SeedGalleryAsync(SeedCatalog c, Dictionary<int, int> haircuts, CancellationToken ct)
    {
        var have = (await db.GalleryItems.Select(g => g.ImageKey).ToListAsync(ct)).ToHashSet();
        var hashes = (await db.GalleryItems.Where(g => g.ContentHash != null).Select(g => g.ContentHash!).ToListAsync(ct)).ToHashSet();
        var now = DateTime.UtcNow;
        var order = 0;
        foreach (var row in c.Gallery.Where(g => !have.Contains(g.ImageKey) && (g.ContentHash is null || hashes.Add(g.ContentHash))))
        {
            db.GalleryItems.Add(new GalleryItem
            {
                ImageKey = row.ImageKey,
                ContentHash = row.ContentHash,
                CategoryId = haircuts[row.CategoryId],
                Caption = row.Caption,
                IsFeatured = row.IsFeatured,
                CreatedAtUtc = now.AddMinutes(-order++),
            });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A salon profile nobody has touched yet gets the real one; one the admin has edited is left alone.</summary>
    private async Task SeedSalonAsync(SeedCatalog c, CancellationToken ct)
    {
        var row = c.Salon.FirstOrDefault();
        if (row is null) return;
        var settings = await db.SalonSettings.SingleAsync(ct);
        if (settings.Description is not null || settings.Address is not null || settings.MapsUrl is not null || settings.LogoKey is not null)
            return;

        settings.Address = row.Address;
        settings.Phone = row.Phone;
        settings.Description = row.Description;
        settings.LogoKey = row.LogoKey;
        settings.MapsUrl = row.MapsUrl;
        settings.Latitude = row.Latitude;
        settings.Longitude = row.Longitude;
        settings.FacebookUrl = row.FacebookUrl;
        settings.InstagramUrl = row.InstagramUrl;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The shop front page of the file, but only on a shop that has no sections yet.</summary>
    private async Task SeedShopSectionsAsync(
        SeedCatalog c, Dictionary<int, int> categories, Dictionary<int, int> brands, CancellationToken ct)
    {
        if (await db.ShopSections.AnyAsync(ct)) return;

        var created = new Dictionary<int, ShopSection>();
        foreach (var row in c.ShopSections.OrderBy(s => s.SortOrder))
        {
            var kind = Enum.Parse<ShopSectionKind>(row.Kind);
            var section = new ShopSection
            {
                Title = row.Title,
                Eyebrow = row.Eyebrow,
                Kind = kind,
                CategoryId = row.CategoryId is { } k ? categories[k] : null,
                BrandId = row.BrandId is { } b ? brands[b] : null,
                MaxItems = row.MaxItems,
                SortOrder = row.SortOrder,
                IsActive = row.IsActive,
                Layout = Enum.Parse<ShopSectionLayout>(row.Layout),
                Background = Enum.Parse<ShopSectionBackground>(row.Background),
                TileSize = Enum.Parse<ShopTileSize>(row.TileSize),
                CardSpan = Enum.Parse<ShopCardSpan>(row.CardSpan),
                HeadingAlign = Enum.Parse<ShopHeadingAlign>(row.HeadingAlign),
                ShowSeeAll = row.ShowSeeAll,
                ShowAsCard = row.ShowAsCard,
                ImageKey = row.ImageKey,
            };
            db.ShopSections.Add(created[row.Id] = section);
        }
        await db.SaveChangesAsync(ct);

        // Tile blocks list the categories or brands the admin picked: translate the ids of the file to ours.
        foreach (var entry in c.ShopSectionEntries)
        {
            var section = created[entry.ShopSectionId];
            var entityId = section.Kind == ShopSectionKind.BrandTiles ? brands[entry.EntityId] : categories[entry.EntityId];
            db.Set<ShopSectionEntry>().Add(new ShopSectionEntry { ShopSectionId = section.Id, EntityId = entityId, SortOrder = entry.SortOrder });
        }
        await db.SaveChangesAsync(ct);
    }
}
