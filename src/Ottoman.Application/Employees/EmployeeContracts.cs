using FluentValidation;
using Ottoman.Application.Auth;
using Ottoman.Application.Common;
using Ottoman.Domain.Scheduling;

namespace Ottoman.Application.Employees;

/// <summary>Email and phone are only populated for admins.</summary>
public record EmployeeDto(
    string Id,
    string FirstName,
    string LastName,
    string FullName,
    string Title,
    string? Bio,
    string? PhotoUrl,
    bool IsActive,
    IReadOnlyList<int> ServiceIds,
    string? Email,
    string? Phone);

public record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Password,
    string Title,
    string? Bio,
    IReadOnlyList<int>? ServiceIds);

public record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string Title,
    string? Bio,
    bool IsActive);

public record SetEmployeeServicesRequest(IReadOnlyList<int> ServiceIds);

/// <summary>Times are local salon wall-clock "HH:mm".</summary>
public record WorkingHoursDto(DayOfWeek DayOfWeek, string Start, string End, string? BreakStart, string? BreakEnd);

public record SetWorkingHoursRequest(IReadOnlyList<WorkingHoursDto> Days);

public record TimeOffDto(int Id, DateTime StartUtc, DateTime EndUtc, string? Reason);

public record CreateTimeOffRequest(DateTime StartUtc, DateTime EndUtc, string? Reason);

public static class HhMm
{
    public static bool TryParse(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out time);

    public static string Format(TimeOnly t) => t.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
}

public class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithName("Ime");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithName("Prezime");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256).WithName("Email");
        RuleFor(x => x.Phone).MaximumLength(30).Matches(@"^[0-9+()\-\s/]*$")
            .When(x => !string.IsNullOrEmpty(x.Phone)).WithName("Telefon");
        RuleFor(x => x.Password).Strong();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100).WithName("Titula");
        RuleFor(x => x.Bio).MaximumLength(500).WithName("Opis");
    }
}

public class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithName("Ime");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithName("Prezime");
        RuleFor(x => x.Phone).MaximumLength(30).Matches(@"^[0-9+()\-\s/]*$")
            .When(x => !string.IsNullOrEmpty(x.Phone)).WithName("Telefon");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100).WithName("Titula");
        RuleFor(x => x.Bio).MaximumLength(500).WithName("Opis");
    }
}

public class SetWorkingHoursRequestValidator : AbstractValidator<SetWorkingHoursRequest>
{
    public SetWorkingHoursRequestValidator()
    {
        RuleFor(x => x.Days).NotNull().WithName("Dani");
        RuleFor(x => x.Days).Must(d => d is null || d.Select(x => x.DayOfWeek).Distinct().Count() == d.Count)
            .WithMessage("Svaki dan se smije pojaviti najviše jednom.");
        RuleForEach(x => x.Days).ChildRules(day =>
        {
            day.RuleFor(d => d).Custom((d, ctx) =>
            {
                if (!HhMm.TryParse(d.Start, out var start) || !HhMm.TryParse(d.End, out var end))
                {
                    ctx.AddFailure("Vrijeme mora biti u formatu HH:mm.");
                    return;
                }
                TimeOnly? bs = null, be = null;
                if (d.BreakStart is not null && !(HhMm.TryParse(d.BreakStart, out var b1) && (bs = b1) is not null))
                    ctx.AddFailure("Početak pauze mora biti u formatu HH:mm.");
                if (d.BreakEnd is not null && !(HhMm.TryParse(d.BreakEnd, out var b2) && (be = b2) is not null))
                    ctx.AddFailure("Kraj pauze mora biti u formatu HH:mm.");

                foreach (var error in new WorkingDay(start, end, bs, be).Validate(5))
                    ctx.AddFailure(error);
            });
        });
    }
}

public class CreateTimeOffRequestValidator : AbstractValidator<CreateTimeOffRequest>
{
    public CreateTimeOffRequestValidator()
    {
        RuleFor(x => x.EndUtc).GreaterThan(x => x.StartUtc).WithMessage("Kraj odsustva mora biti poslije početka.");
        RuleFor(x => x.Reason).MaximumLength(200);
    }
}

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeDto>> ListAsync(bool includeInactive, bool includePrivate, CancellationToken ct);
    Task<Result<EmployeeDto>> GetAsync(string id, bool includePrivate, CancellationToken ct);
    Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeRequest request, CancellationToken ct);
    Task<Result<EmployeeDto>> UpdateAsync(string id, UpdateEmployeeRequest request, CancellationToken ct);
    Task<Result<EmployeeDto>> SetServicesAsync(string id, IReadOnlyList<int> serviceIds, CancellationToken ct);

    Task<Result<IReadOnlyList<WorkingHoursDto>>> GetWorkingHoursAsync(string id, CancellationToken ct);
    Task<Result<IReadOnlyList<WorkingHoursDto>>> SetWorkingHoursAsync(string id, SetWorkingHoursRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<TimeOffDto>>> ListTimeOffAsync(string id, CancellationToken ct);
    Task<Result<TimeOffDto>> AddTimeOffAsync(string id, CreateTimeOffRequest request, CancellationToken ct);
    Task<Result<bool>> RemoveTimeOffAsync(string id, int timeOffId, CancellationToken ct);
}
