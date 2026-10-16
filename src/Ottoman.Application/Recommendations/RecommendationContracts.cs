using Ottoman.Application.Catalog;
using Ottoman.Application.Employees;
using Ottoman.Application.Shop;

namespace Ottoman.Application.Recommendations;

/// <summary>
/// Content-based recommendations ("you might like"). The same engine runs three times: over products, services and barbers.
/// Everything returned is currently available (active, and in stock for products) and carries the reason it was chosen.
/// </summary>
public interface IRecommendationService
{
    Task<IReadOnlyList<Recommended<ProductDto>>> ProductsAsync(string userId, int count, CancellationToken ct);
    Task<IReadOnlyList<Recommended<ServiceDto>>> ServicesAsync(string userId, int count, CancellationToken ct);
    Task<IReadOnlyList<Recommended<EmployeeDto>>> EmployeesAsync(string userId, int count, CancellationToken ct);

    /// <summary>"Similar products" for a detail page.</summary>
    Task<IReadOnlyList<Recommended<ProductDto>>> RelatedProductsAsync(int productId, int count, CancellationToken ct);
}

/// <summary>
/// The item vectors are built from names, descriptions, brands and categories, so they only change when the catalogue
/// does. Whoever changes a product, service or barber calls this; stock and ratings are always read fresh.
/// </summary>
public interface IRecommendationCache
{
    void Invalidate();
}
