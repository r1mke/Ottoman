namespace Ottoman.Domain.Entities;

public class Service
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public string? ImageKey { get; set; }
    public bool IsActive { get; set; } = true;

    public List<EmployeeService> Employees { get; set; } = [];
}

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Storage key of the brand logo; without one the shop shows a photo of one of its products.</summary>
    public string? LogoKey { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal Price { get; set; }

    /// <summary>Discounted price while the product is on sale (always below <see cref="Price"/>); null = no sale.</summary>
    public decimal? SalePrice { get; set; }

    /// <summary>What the customer pays: the sale price when there is one.</summary>
    public decimal EffectivePrice => SalePrice ?? Price;

    public int Stock { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public List<ProductImage> Images { get; set; } = [];
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ShopSectionKind
{
    /// <summary>The most recently added products.</summary>
    NewArrivals = 0,
    /// <summary>Products that have a sale price.</summary>
    OnSale = 1,
    /// <summary>Products of one category.</summary>
    Category = 2,
    /// <summary>Products the admin picked by hand.</summary>
    Manual = 3,
    /// <summary>The block of category tiles (not a list of products).</summary>
    CategoryTiles = 4,
    /// <summary>The block of brand tiles.</summary>
    BrandTiles = 5,
    /// <summary>The "you may like" block, filled per customer.</summary>
    Recommended = 6,
}

public static class ShopSectionKinds
{
    /// <summary>Blocks that are not a list of chosen products: they have their own content.</summary>
    public static bool IsBuiltIn(ShopSectionKind kind) =>
        kind is ShopSectionKind.CategoryTiles or ShopSectionKind.BrandTiles or ShopSectionKind.Recommended;
}

/// <summary>How a section lays out its products on the shop page.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ShopSectionLayout
{
    /// <summary>One row that scrolls sideways.</summary>
    Row = 0,
    /// <summary>Two columns, product under product (for short sections).</summary>
    Grid = 1,
    /// <summary>A wide picture with the title over it, and the row of products under it.</summary>
    Banner = 2,
}

/// <summary>The ground a section stands on. A banner always has one, so it reads as one piece with its products.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ShopSectionBackground
{
    /// <summary>The plain page.</summary>
    None = 0,
    /// <summary>A soft dark panel.</summary>
    Soft = 1,
    /// <summary>A panel with a warm amber tint.</summary>
    Amber = 2,
}

/// <summary>How big the product cards of a section are.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ShopTileSize
{
    Compact = 0,
    Normal = 1,
    Large = 2,
}

/// <summary>How wide a section card is on the entry page: half of the page, or all of it.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ShopCardSpan
{
    Half = 0,
    Full = 1,
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ShopHeadingAlign
{
    Left = 0,
    Center = 1,
}

/// <summary>A row on the shop front page ("Novo u ponudi", "Akcije"...), arranged by the admin.</summary>
public class ShopSection
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;

    /// <summary>The small amber text above the title (optional). The title is the large white text; the bar under it is automatic.</summary>
    public string? Eyebrow { get; set; }
    public ShopSectionKind Kind { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Optional second filter of a category section: only products of this brand (alone or with the category).</summary>
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public int MaxItems { get; set; } = 10;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ShopSectionLayout Layout { get; set; } = ShopSectionLayout.Row;

    // ---- look
    public ShopSectionBackground Background { get; set; } = ShopSectionBackground.None;
    public ShopTileSize TileSize { get; set; } = ShopTileSize.Normal;
    public ShopCardSpan CardSpan { get; set; } = ShopCardSpan.Half;
    public ShopHeadingAlign HeadingAlign { get; set; } = ShopHeadingAlign.Left;

    /// <summary>A "see all" link under the section that opens the whole list.</summary>
    public bool ShowSeeAll { get; set; }

    /// <summary>Also show the section as a card on the shop entry page (and not only as a row of products).</summary>
    public bool ShowAsCard { get; set; }

    /// <summary>Storage key of a picture the admin uploaded for the card.</summary>
    public string? ImageKey { get; set; }

    /// <summary>Alternatively the id of a product picture the admin picked. Deliberately not a foreign key: if that
    /// picture is deleted the card simply falls back to the automatic cover.</summary>
    public int? ImageProductImageId { get; set; }

    public List<ShopSectionProduct> Products { get; set; } = [];

    /// <summary>The categories or brands the admin picked for a tile block, in the order to show them. Empty means automatic.</summary>
    public List<ShopSectionEntry> Entries { get; set; } = [];
}

/// <summary>One category or brand picked for a tile block (<see cref="EntityId"/> is the category or brand id).</summary>
public class ShopSectionEntry
{
    public int ShopSectionId { get; set; }
    public int EntityId { get; set; }
    public int SortOrder { get; set; }
}

public class ShopSectionProduct
{
    public int ShopSectionId { get; set; }
    public int ProductId { get; set; }
    public int SortOrder { get; set; }
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string FileKey { get; set; } = null!;
    public int SortOrder { get; set; }

    /// <summary>SHA-256 of the uploaded bytes (hex). Lets the same picture be refused twice for one product.
    /// Null on pictures uploaded before this existed.</summary>
    public string? ContentHash { get; set; }
}
