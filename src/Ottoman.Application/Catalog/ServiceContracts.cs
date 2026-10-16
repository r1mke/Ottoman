using FluentValidation;
using Ottoman.Application.Common;

namespace Ottoman.Application.Catalog;

public record ServiceDto(
    int Id,
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes,
    string? ImageUrl,
    bool IsActive);

public record ServiceUpsertRequest(
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes,
    bool IsActive = true);

public class ServiceUpsertRequestValidator : AbstractValidator<ServiceUpsertRequest>
{
    public ServiceUpsertRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).WithName("Naziv");
        RuleFor(x => x.Description).MaximumLength(1000).WithName("Opis");
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(10_000).PrecisionScale(18, 2, false).WithName("Cijena");
        RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 480)
            .Must(d => d % 5 == 0).WithMessage("Trajanje mora biti višekratnik od 5 minuta.")
            .WithName("Trajanje");
    }
}

public interface IServiceCatalog
{
    /// <param name="employeeId">When set, only services this employee performs.</param>
    Task<IReadOnlyList<ServiceDto>> ListAsync(string? employeeId, bool includeInactive, CancellationToken ct);

    Task<Result<ServiceDto>> GetAsync(int id, CancellationToken ct);
    Task<Result<ServiceDto>> CreateAsync(ServiceUpsertRequest request, CancellationToken ct);
    Task<Result<ServiceDto>> UpdateAsync(int id, ServiceUpsertRequest request, CancellationToken ct);

    /// <summary>Soft delete: past reservations keep their snapshot, so the row is never removed.</summary>
    Task<Result<bool>> DeactivateAsync(int id, CancellationToken ct);
}
