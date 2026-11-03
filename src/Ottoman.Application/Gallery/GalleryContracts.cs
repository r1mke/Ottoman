using FluentValidation;
using Ottoman.Application.Common;

namespace Ottoman.Application.Gallery;

public record HaircutCategoryDto(int Id, string Name, int Count);

public record GalleryItemDto(
    int Id,
    string ImageUrl,
    int CategoryId,
    string CategoryName,
    string? EmployeeId,
    string? EmployeeName,
    string? Caption,
    bool IsFeatured,
    DateTime CreatedAtUtc);

public record HaircutCategoryRequest(string Name);

public class HaircutCategoryRequestValidator : AbstractValidator<HaircutCategoryRequest>
{
    public HaircutCategoryRequestValidator() =>
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithName("Naziv");
}

/// <summary>What the admin can change about a picture already in the gallery (the picture itself is replaced by deleting it).</summary>
public record UpdateGalleryItemRequest(int CategoryId, string? EmployeeId, string? Caption, bool IsFeatured);

public class UpdateGalleryItemRequestValidator : AbstractValidator<UpdateGalleryItemRequest>
{
    public UpdateGalleryItemRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Odaberite kategoriju frizure.");
        RuleFor(x => x.Caption).MaximumLength(200).WithName("Opis");
    }
}

public interface IGalleryService
{
    Task<PagedResult<HaircutCategoryDto>> CategoriesAsync(PageQuery page, CancellationToken ct);
    Task<Result<HaircutCategoryDto>> CreateCategoryAsync(HaircutCategoryRequest request, CancellationToken ct);
    Task<Result<HaircutCategoryDto>> RenameCategoryAsync(int id, HaircutCategoryRequest request, CancellationToken ct);

    /// <summary>Refused while any picture still uses the category.</summary>
    Task<Result<bool>> DeleteCategoryAsync(int id, CancellationToken ct);

    Task<PagedResult<GalleryItemDto>> PageAsync(int? categoryId, bool? featured, string? employeeId, PageQuery page, CancellationToken ct);
    Task<Result<GalleryItemDto>> GetAsync(int id, CancellationToken ct);
    Task<Result<GalleryItemDto>> UpdateAsync(int id, UpdateGalleryItemRequest request, CancellationToken ct);
}
