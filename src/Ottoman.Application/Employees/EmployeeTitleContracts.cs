using FluentValidation;
using Ottoman.Application.Common;

namespace Ottoman.Application.Employees;

public record EmployeeTitleDto(int Id, string Name, int InUse);

public record AddEmployeeTitleRequest(string Name);

public record UpdateEmployeeTitleRequest(string Name);

/// <summary>The pick list of barber titles. Admin-only at the HTTP edge.</summary>
public interface IEmployeeTitles
{
    Task<PagedResult<EmployeeTitleDto>> ListAsync(PageQuery page, CancellationToken ct);

    Task<Result<EmployeeTitleDto>> AddAsync(string name, CancellationToken ct);

    /// <summary>Renames a title; the barbers who have it keep it under the new name.</summary>
    Task<Result<EmployeeTitleDto>> UpdateAsync(int id, string name, CancellationToken ct);

    /// <summary>Refused while a barber still has the title, so the list never loses a title that is in use.</summary>
    Task<Result<bool>> DeleteAsync(int id, CancellationToken ct);

    /// <summary>
    /// The id of a title on the list: <paramref name="id"/> when given, otherwise the title with this name (case does
    /// not matter). Null when there is no such title; nothing is ever added here.
    /// </summary>
    Task<int?> FindAsync(int? id, string? name, CancellationToken ct);
}

public class AddEmployeeTitleRequestValidator : AbstractValidator<AddEmployeeTitleRequest>
{
    public AddEmployeeTitleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithName("Titula");
    }
}

public class UpdateEmployeeTitleRequestValidator : AbstractValidator<UpdateEmployeeTitleRequest>
{
    public UpdateEmployeeTitleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithName("Titula");
    }
}
