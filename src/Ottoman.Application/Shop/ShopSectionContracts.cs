using FluentValidation;
using Ottoman.Application.Common;
using Ottoman.Domain.Entities;

namespace Ottoman.Application.Shop;

/// <summary>How the admin sees a section: the rule, not the products it currently resolves to.</summary>
public record ShopSectionAdminDto(
    int Id,
    string Title,
    ShopSectionKind Kind,
    int? CategoryId,
    string? CategoryName,
    int? BrandId,
    string? BrandName,
    int MaxItems,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<int> ProductIds,
    bool ShowAsCard = false,
    string? ImageUrl = null,
    int? ImageProductImageId = null,
    ShopSectionLayout Layout = ShopSectionLayout.Row,
    string? Eyebrow = null,
    int ProductCount = 0,
    IReadOnlyList<int>? EntryIds = null,
    ShopSectionStyle? Style = null);

public record ShopSectionRequest(
    string Title,
    ShopSectionKind Kind,
    int? CategoryId,
    int MaxItems = 10,
    bool IsActive = true,
    IReadOnlyList<int>? ProductIds = null,
    int? BrandId = null,
    bool ShowAsCard = false,
    ShopSectionLayout Layout = ShopSectionLayout.Row,
    string? Eyebrow = null,
    IReadOnlyList<int>? EntryIds = null,
    ShopSectionStyle? Style = null);

/// <summary>Use this picture of a product as the card picture.</summary>
public record ShopSectionPickedImageRequest(int ProductImageId);

public class ShopSectionPickedImageRequestValidator : AbstractValidator<ShopSectionPickedImageRequest>
{
    public ShopSectionPickedImageRequestValidator() =>
        RuleFor(x => x.ProductImageId).GreaterThan(0).WithMessage("Odaberite sliku proizvoda.").WithName("Slika");
}

public record ShopSectionOrderRequest(IReadOnlyList<int> Ids);

public class ShopSectionOrderRequestValidator : AbstractValidator<ShopSectionOrderRequest>
{
    public const int MaxSections = 200;

    public ShopSectionOrderRequestValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().WithMessage("Pošaljite redoslijed sekcija.").WithName("Redoslijed");
        RuleFor(x => x.Ids)
            .Must(ids => ids.Count <= MaxSections).WithMessage($"Najviše {MaxSections} sekcija.")
            .Must(ids => ids.All(id => id > 0)).WithMessage("Neispravna oznaka sekcije.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Ista sekcija je navedena više puta.")
            .When(x => x.Ids is { Count: > 0 })
            .WithName("Redoslijed");
    }
}

/// <summary>How a section or its card looks, apart from what it holds.</summary>
public record ShopSectionStyle(
    ShopSectionBackground Background = ShopSectionBackground.None,
    ShopTileSize TileSize = ShopTileSize.Normal,
    ShopCardSpan CardSpan = ShopCardSpan.Half,
    ShopHeadingAlign HeadingAlign = ShopHeadingAlign.Left,
    bool ShowSeeAll = false);

/// <summary>One block of the shop entry page, in the order the admin arranged, ready to draw.</summary>
/// <param name="Products">The products of a product block (a section).</param>
/// <param name="Tiles">The categories or brands of a tile block.</param>
public record ShopBlockDto(
    int SectionId,
    ShopSectionKind Kind,
    string Title,
    string? Eyebrow,
    ShopSectionLayout Layout,
    string? ImageUrl,
    IReadOnlyList<ProductDto>? Products,
    IReadOnlyList<ShopTileDto>? Tiles,
    ShopSectionStyle? Style = null);

/// <summary>What a section or card title may look like. A card is about half a phone screen wide, so a long title either
/// runs off it or wraps into a tall, ugly block; keeping titles short keeps every card and heading tidy.</summary>
public static class ShopSectionTitle
{
    public const int MinLength = 2;
    public const int MaxLength = 24;

    /// <summary>The small amber text above the title may be as long as the title, and may be left out.</summary>
    public const int EyebrowMaxLength = 24;

    /// <summary>The most category or brand tiles a tile block may show.</summary>
    public const int MaxTiles = 30;

    /// <summary>Trims the title and collapses runs of spaces, tabs and line breaks into one space.</summary>
    public static string Normalize(string? title) =>
        System.Text.RegularExpressions.Regex.Replace((title ?? "").Trim(), @"\s+", " ");
}

public class ShopSectionRequestValidator : AbstractValidator<ShopSectionRequest>
{
    public ShopSectionRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithName("Naslov");
        RuleFor(x => ShopSectionTitle.Normalize(x.Title)).Must(t => t.Length >= ShopSectionTitle.MinLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Title))
            .WithMessage($"Naslov mora imati najmanje {ShopSectionTitle.MinLength} znaka.").OverridePropertyName("Naslov");
        RuleFor(x => ShopSectionTitle.Normalize(x.Title)).Must(t => t.Length <= ShopSectionTitle.MaxLength)
            .WithMessage($"Naslov može imati najviše {ShopSectionTitle.MaxLength} znaka, da lijepo stane na karticu.")
            .OverridePropertyName("Naslov");
        RuleFor(x => x.Kind).IsInEnum().WithName("Vrsta");
        RuleFor(x => x.Layout).IsInEnum().WithName("Oblik");
        RuleFor(x => ShopSectionTitle.Normalize(x.Eyebrow)).Must(t => t.Length <= ShopSectionTitle.EyebrowMaxLength)
            .WithMessage($"Žuti tekst može imati najviše {ShopSectionTitle.EyebrowMaxLength} znaka.")
            .OverridePropertyName("Žuti tekst");
        RuleFor(x => x.MaxItems).InclusiveBetween(1, ShopSectionTitle.MaxTiles).WithName("Broj prikazanih");
        // Category tiles sit in two columns: an even number keeps the last row full.
        RuleFor(x => x.MaxItems).Must(n => n % 2 == 0)
            .When(x => x.Kind == ShopSectionKind.CategoryTiles && (x.EntryIds is null || x.EntryIds.Count == 0))
            .WithMessage("Broj kategorija mora biti paran, da zadnji red bude pun.").WithName("Broj prikazanih");
        RuleFor(x => x.EntryIds).Must(ids => ids is null || ids.Count == 0 || ids.Count % 2 == 0)
            .When(x => x.Kind == ShopSectionKind.CategoryTiles)
            .WithMessage("Broj odabranih kategorija mora biti paran, da zadnji red bude pun.").WithName("Kategorije");
        RuleFor(x => x.EntryIds).Must(ids => ids is null || (ids.Count <= ShopSectionTitle.MaxTiles && ids.Distinct().Count() == ids.Count))
            .When(x => x.Kind is ShopSectionKind.CategoryTiles or ShopSectionKind.BrandTiles)
            .WithMessage("Odabrane stavke moraju biti različite i ne više od 30.").WithName("Stavke");
        RuleFor(x => x).Must(x => x.CategoryId.HasValue || x.BrandId.HasValue)
            .When(x => x.Kind == ShopSectionKind.Category)
            .WithMessage("Odaberite kategoriju ili brend.").OverridePropertyName("Kategorija");
        RuleFor(x => x.ProductIds).NotEmpty().When(x => x.Kind == ShopSectionKind.Manual)
            .WithMessage("Odaberite barem jedan proizvod.").WithName("Proizvodi");
        RuleFor(x => x.ProductIds).Must(ids => ids is null || ids.Count <= 30)
            .WithMessage("Najviše 30 proizvoda u sekciji.");
    }
}

public interface IShopSectionService
{
    Task<PagedResult<ShopSectionAdminDto>> ListForAdminAsync(PageQuery page, CancellationToken ct);
    Task<Result<ShopSectionAdminDto>> CreateAsync(ShopSectionRequest request, CancellationToken ct);
    Task<Result<ShopSectionAdminDto>> UpdateAsync(int id, ShopSectionRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteAsync(int id, CancellationToken ct);
    Task<Result<bool>> ReorderAsync(IReadOnlyList<int> ids, CancellationToken ct);

    Task<Result<ShopSectionAdminDto>> GetAsync(int id, CancellationToken ct);

    /// <summary>Picks a product picture for the card (an uploaded picture is dropped).</summary>
    Task<Result<ShopSectionAdminDto>> PickImageAsync(int id, int productImageId, CancellationToken ct);

    /// <summary>All products a section currently resolves to (what its card opens).</summary>
    Task<Result<PagedResult<ProductDto>>> GetProductsAsync(int id, PageQuery page, CancellationToken ct);

    /// <summary>The blocks of the entry page in the admin's order. Empty ones are left out.</summary>
    /// <param name="categoryTiles">All categories with products on the shelf, busiest first.</param>
    /// <param name="brandTiles">All brands with products on the shelf, busiest first.</param>
    Task<IReadOnlyList<ShopBlockDto>> GetBlocksAsync(
        IReadOnlyList<ShopTileDto> categoryTiles, IReadOnlyList<ShopTileDto> brandTiles, CancellationToken ct);

    /// <summary>The sections shown as cards on the entry page, with picture and numbers.</summary>
    Task<IReadOnlyList<ShopCardDto>> GetCardsAsync(CancellationToken ct);
}
