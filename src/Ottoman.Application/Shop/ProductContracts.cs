using FluentValidation;
using Ottoman.Application.Common;

namespace Ottoman.Application.Shop;

public record BrandDto(int Id, string Name);

public record CategoryDto(int Id, string Name, string? Description);

public record NameRequest(string Name, string? Description = null);

public class NameRequestValidator : AbstractValidator<NameRequest>
{
    public NameRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithName("Naziv");
        RuleFor(x => x.Description).MaximumLength(500).WithName("Opis");
    }
}

public record ProductDto(
    int Id,
    string Name,
    string? Description,
    int? BrandId,
    string? BrandName,
    int? CategoryId,
    string? CategoryName,
    decimal Price,
    int Stock,
    int? LowStockThreshold,
    bool IsLowStock,
    bool IsActive,
    string? ImageUrl,
    IReadOnlyList<ProductImageDto> Images);

public record ProductImageDto(int Id, string Url);

/// <summary>Stock is set here only when the product is created; afterwards use the atomic adjust endpoint.</summary>
public record ProductUpsertRequest(
    string Name,
    string? Description,
    int? BrandId,
    int? CategoryId,
    decimal Price,
    int Stock,
    int? LowStockThreshold,
    bool IsActive = true);

public class ProductUpsertRequestValidator : AbstractValidator<ProductUpsertRequest>
{
    public ProductUpsertRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithName("Naziv");
        RuleFor(x => x.Description).MaximumLength(2000).WithName("Opis");
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(100_000).PrecisionScale(18, 2, false).WithName("Cijena");
        RuleFor(x => x.Stock).InclusiveBetween(0, 100_000).WithName("Zaliha");
        RuleFor(x => x.LowStockThreshold).InclusiveBetween(0, 100_000).When(x => x.LowStockThreshold.HasValue).WithName("Prag zalihe");
    }
}

/// <param name="Delta">Positive = restock, negative = write-off. Never lets the stock go below zero.</param>
public record StockAdjustRequest(int Delta);

public class StockAdjustRequestValidator : AbstractValidator<StockAdjustRequest>
{
    public StockAdjustRequestValidator() =>
        RuleFor(x => x.Delta).NotEqual(0).InclusiveBetween(-100_000, 100_000).WithName("Promjena zalihe");
}

public enum ProductSort
{
    Name,
    PriceAsc,
    PriceDesc,
    Newest,
}

public record ProductQuery(
    string? Search = null,
    int? CategoryId = null,
    int? BrandId = null,
    ProductSort Sort = ProductSort.Name,
    bool IncludeInactive = false,
    bool OnlyLowStock = false);

public interface IProductCatalog
{
    Task<PagedResult<ProductDto>> ListAsync(ProductQuery query, PageQuery page, CancellationToken ct);
    Task<Result<ProductDto>> GetAsync(int id, bool includeInactive, CancellationToken ct);

    /// <summary>Active products by id, in the order of <paramref name="ids"/> (used to show ranked recommendations).</summary>
    Task<IReadOnlyList<ProductDto>> GetManyAsync(IReadOnlyList<int> ids, CancellationToken ct);
    Task<Result<ProductDto>> CreateAsync(ProductUpsertRequest request, CancellationToken ct);
    Task<Result<ProductDto>> UpdateAsync(int id, ProductUpsertRequest request, CancellationToken ct);
    Task<Result<ProductDto>> AdjustStockAsync(int id, int delta, CancellationToken ct);
    Task<Result<bool>> DeactivateAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<BrandDto>> ListBrandsAsync(CancellationToken ct);
    Task<Result<BrandDto>> CreateBrandAsync(NameRequest request, CancellationToken ct);
    Task<Result<BrandDto>> UpdateBrandAsync(int id, NameRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteBrandAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct);
    Task<Result<CategoryDto>> CreateCategoryAsync(NameRequest request, CancellationToken ct);
    Task<Result<CategoryDto>> UpdateCategoryAsync(int id, NameRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteCategoryAsync(int id, CancellationToken ct);
}
