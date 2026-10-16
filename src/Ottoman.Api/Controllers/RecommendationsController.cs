using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Catalog;
using Ottoman.Application.Employees;
using Ottoman.Application.Recommendations;
using Ottoman.Application.Shop;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class RecommendationsController(IRecommendationService recommendations) : ControllerBase
{
    private static int Clamp(int count) => Math.Clamp(count, 1, 20);

    [HttpGet("recommendations/products")]
    public Task<IReadOnlyList<ProductDto>> Products([FromQuery] int count = 6, CancellationToken ct = default) =>
        recommendations.ProductsAsync(User.UserId(), Clamp(count), ct);

    [HttpGet("recommendations/services")]
    public Task<IReadOnlyList<ServiceDto>> Services([FromQuery] int count = 6, CancellationToken ct = default) =>
        recommendations.ServicesAsync(User.UserId(), Clamp(count), ct);

    [HttpGet("recommendations/employees")]
    public Task<IReadOnlyList<EmployeeDto>> Employees([FromQuery] int count = 4, CancellationToken ct = default) =>
        recommendations.EmployeesAsync(User.UserId(), Clamp(count), ct);

    /// <summary>Products similar to this one, for the "Slično" row on a product page.</summary>
    [HttpGet("products/{id:int}/related")]
    public Task<IReadOnlyList<ProductDto>> Related(int id, [FromQuery] int count = 4, CancellationToken ct = default) =>
        recommendations.RelatedProductsAsync(id, Clamp(count), ct);
}
