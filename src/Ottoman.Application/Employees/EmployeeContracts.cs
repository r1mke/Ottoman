using FluentValidation;
using Ottoman.Application.Auth;
using Ottoman.Application.Common;
using Ottoman.Domain.Scheduling;

namespace Ottoman.Application.Employees;

/// <summary>Email, phone and the sign-in lock (LockedUntilUtc, after too many wrong passwords) are only populated for admins.</summary>
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
    string? Phone,
    string? Gender = null,
    double PhotoZoom = 1.0,
    int TitleId = 0,
    DateTime? LockedUntilUtc = null);

public record CreateEmployeeRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string Password,
    string Title,
    string? Bio,
    IReadOnlyList<int>? ServiceIds,
    string? Gender = null);

public record UpdateEmployeeRequest(
    string FirstName,
    string LastName,
    string? Phone,
    string Title,
    string? Bio,
    bool IsActive,
    string? Gender = null,
    double? PhotoZoom = null);

/// <summary>What a barber may change about themselves. Title, services, working hours and status stay with the admin.</summary>
public record UpdateOwnEmployeeProfileRequest(string? Gender, string? Bio, double? PhotoZoom);

/// <summary>"Male" or "Female"; null means leave as it is (update) or not said (create).</summary>
public static class GenderNames
{
    public static bool IsValid(string? value) => value is null or "Male" or "Female";

    public static Ottoman.Domain.Gender? Parse(string? value) =>
        Enum.TryParse<Ottoman.Domain.Gender>(value, ignoreCase: false, out var g) ? g : null;
}

public record SetEmployeeServicesRequest(IReadOnlyList<int> ServiceIds);

public class SetEmployeeServicesRequestValidator : AbstractValidator<SetEmployeeServicesRequest>
{
    public const int MaxServices = 100;

    public SetEmployeeServicesRequestValidator()
    {
        RuleFor(x => x.ServiceIds).NotNull().WithName("Usluge");
        RuleFor(x => x.ServiceIds)
            .Must(ids => ids.Count <= MaxServices).WithMessage($"Najviše {MaxServices} usluga.")
            .Must(ids => ids.All(id => id > 0)).WithMessage("Neispravna oznaka usluge.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Ista usluga je navedena više puta.")
            .When(x => x.ServiceIds is not null)
            .WithName("Usluge");
    }
}

/// <summary>Times are local salon wall-clock "HH:mm".</summary>
public record WorkingHoursDto(DayOfWeek DayOfWeek, string Start, string End, string? BreakStart, string? BreakEnd);

public record SetWorkingHoursRequest(IReadOnlyList<WorkingHoursDto> Days);

public record TimeOffDto(int Id, DateTime StartUtc, DateTime EndUtc, string? Reason);

/// <param name="Force">
/// Add the time off even though it overlaps active bookings. Without it such a request is refused with the list of
/// those bookings, so the admin decides knowingly; with it the bookings stay as they are and their clients are told.
/// </param>
public record CreateTimeOffRequest(DateTime StartUtc, DateTime EndUtc, string? Reason, bool Force = false);

/// <summary>An active booking (pending or confirmed) that falls into a requested time off.</summary>
public record TimeOffClashDto(int ReservationId, string ClientName, DateTime StartUtc, DateTime EndUtc, string LocalStart);

/// <summary>The created time off, or why not; <see cref="Clashes"/> lists the bookings it overlaps (refused or forced).</summary>
public record TimeOffOutcome(Result<TimeOffDto> Result, IReadOnlyList<TimeOffClashDto> Clashes);

public static class TimeOffClashes
{
    /// <summary>The refusal shown to the admin: how many bookings, who and when, and what to do.</summary>
    public static string Message(IReadOnlyList<TimeOffClashDto> clashes)
    {
        const int shown = 5;
        var list = string.Join("; ", clashes.Take(shown).Select(c => $"{c.ClientName}, {c.LocalStart}"));
        var more = clashes.Count > shown ? $" i još {clashes.Count - shown}" : "";
        return $"Odsustvo se preklapa s aktivnim rezervacijama ({clashes.Count}): {list}{more}. " +
               "Pomjerite ili otkažite te termine, ili potvrdite da se odsustvo ipak doda (klijenti će biti obaviješteni).";
    }
}

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
        RuleFor(x => x.Phone).BosnianPhone().WithName("Telefon");
        RuleFor(x => x.Password).Strong();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100).WithName("Titula");
        RuleFor(x => x.Gender).Must(GenderNames.IsValid).WithMessage("Spol mora biti Male ili Female.");
        RuleFor(x => x.Bio).MaximumLength(500).WithName("Opis");
    }
}

public class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).WithName("Ime");
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).WithName("Prezime");
        RuleFor(x => x.Phone).BosnianPhone().WithName("Telefon");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(100).WithName("Titula");
        RuleFor(x => x.Gender).Must(GenderNames.IsValid).WithMessage("Spol mora biti Male ili Female.");
        RuleFor(x => x.PhotoZoom).Must(Ottoman.Application.Images.ImageRules.ValidZoom)
            .WithMessage("Zoom mora biti između 1 i 2.");
        RuleFor(x => x.Bio).MaximumLength(500).WithName("Opis");
    }
}

public class UpdateOwnEmployeeProfileRequestValidator : AbstractValidator<UpdateOwnEmployeeProfileRequest>
{
    public UpdateOwnEmployeeProfileRequestValidator()
    {
        RuleFor(x => x.Gender).Must(GenderNames.IsValid).WithMessage("Spol mora biti Male ili Female.");
        RuleFor(x => x.PhotoZoom).Must(Ottoman.Application.Images.ImageRules.ValidZoom)
            .WithMessage("Zoom mora biti između 1 i 2.");
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

    /// <summary>The same list, one page at a time, with an optional search by name or title.</summary>
    Task<PagedResult<EmployeeDto>> PageAsync(bool includeInactive, bool includePrivate, string? search, PageQuery page, CancellationToken ct);
    Task<Result<EmployeeDto>> GetAsync(string id, bool includePrivate, CancellationToken ct);
    Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeRequest request, CancellationToken ct);
    /// <summary>A barber changing their own gender, bio and photo zoom. NotFound for anybody without a barber profile.</summary>
    Task<Result<bool>> UpdateOwnProfileAsync(string userId, UpdateOwnEmployeeProfileRequest request, CancellationToken ct);

    Task<Result<EmployeeDto>> UpdateAsync(string id, UpdateEmployeeRequest request, CancellationToken ct);
    Task<Result<EmployeeDto>> SetServicesAsync(string id, IReadOnlyList<int> serviceIds, CancellationToken ct);

    Task<Result<IReadOnlyList<WorkingHoursDto>>> GetWorkingHoursAsync(string id, CancellationToken ct);
    Task<Result<IReadOnlyList<WorkingHoursDto>>> SetWorkingHoursAsync(string id, SetWorkingHoursRequest request, CancellationToken ct);

    Task<Result<PagedResult<TimeOffDto>>> PageTimeOffAsync(string id, PageQuery page, CancellationToken ct);
    Task<TimeOffOutcome> AddTimeOffAsync(string id, CreateTimeOffRequest request, CancellationToken ct);
    Task<Result<bool>> RemoveTimeOffAsync(string id, int timeOffId, CancellationToken ct);
}
