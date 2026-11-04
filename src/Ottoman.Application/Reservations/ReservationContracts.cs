using FluentValidation;
using Ottoman.Application.Common;
using Ottoman.Domain;

namespace Ottoman.Application.Reservations;

public record ReservationItemDto(int ServiceId, string Name, decimal Price, int DurationMinutes);

/// <param name="LocalStart">Salon-local "yyyy-MM-dd HH:mm" for display.</param>
/// <param name="CanCancel">Whether the calling user may cancel it right now.</param>
public record ReservationDto(
    int Id,
    string ClientId,
    string ClientName,
    string EmployeeId,
    string EmployeeName,
    DateTime StartUtc,
    DateTime EndUtc,
    string LocalStart,
    string Status,
    string? Note,
    decimal TotalPrice,
    int TotalDurationMinutes,
    IReadOnlyList<ReservationItemDto> Items,
    bool CanCancel,
    DateTime CreatedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancelReason,
    bool CanConfirm = false,
    bool CanDecline = false,
    DateTime? ConfirmedAtUtc = null,
    string? ConfirmedBy = null,
    DateTime? CompletedAtUtc = null,
    string? CompletedBy = null,
    bool Declined = false);

/// <param name="ClientId">Only honoured for admins booking on behalf of a client (walk-ins).</param>
public record CreateReservationRequest(
    string EmployeeId,
    IReadOnlyList<int> ServiceIds,
    DateTime StartUtc,
    string? Note,
    string? ClientId = null);

public record CancelReservationRequest(string? Reason);

/// <summary>Declining always needs a reason: the client gets it in the notification.</summary>
public record DeclineReservationRequest(string Reason);

public class DeclineReservationRequestValidator : AbstractValidator<DeclineReservationRequest>
{
    public DeclineReservationRequestValidator() =>
        RuleFor(x => x.Reason).Must(r => r is not null && r.Trim().Length >= Ottoman.Domain.Reservations.CancellationRules.MinReasonLength)
            .WithMessage($"Unesite razlog (najmanje {Ottoman.Domain.Reservations.CancellationRules.MinReasonLength} znaka). Klijent ga dobija u obavijesti.")
            .MaximumLength(Ottoman.Domain.Reservations.CancellationRules.MaxReasonLength)
            .WithName("Razlog");
}

public record RescheduleReservationRequest(DateTime StartUtc, string? EmployeeId = null);

public record ReservationFilter(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    string? EmployeeId = null,
    string? ClientId = null,
    ReservationStatus? Status = null,
    bool? Upcoming = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);

public record ReservationSummaryDto(int Upcoming, int Completed, int Cancelled, int NoShow, int TotalClients);

public class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.ServiceIds).NotNull().NotEmpty().WithMessage("Odaberite barem jednu uslugu.");
        RuleFor(x => x.StartUtc).Must(d => d.Kind == DateTimeKind.Utc)
            .WithMessage("Vrijeme početka mora biti u UTC formatu (završava sa Z).");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public class CancelReservationRequestValidator : AbstractValidator<CancelReservationRequest>
{
    public CancelReservationRequestValidator() => RuleFor(x => x.Reason).MaximumLength(300);
}

public class RescheduleReservationRequestValidator : AbstractValidator<RescheduleReservationRequest>
{
    public RescheduleReservationRequestValidator() =>
        RuleFor(x => x.StartUtc).Must(d => d.Kind == DateTimeKind.Utc)
            .WithMessage("Vrijeme početka mora biti u UTC formatu (završava sa Z).");
}

public interface IReservationService
{
    Task<Result<ReservationDto>> CreateAsync(Actor actor, CreateReservationRequest request, CancellationToken ct);

    /// <summary>Visible to the owning client, the assigned employee and admins; anyone else gets NotFound.</summary>
    Task<Result<ReservationDto>> GetAsync(Actor actor, int id, CancellationToken ct);

    /// <summary>
    /// Admins see everything matching the filter; employees are pinned to their own schedule;
    /// clients are pinned to their own reservations.
    /// </summary>
    Task<PagedResult<ReservationDto>> ListAsync(Actor actor, ReservationFilter filter, PageQuery page, CancellationToken ct);

    Task<Result<ReservationDto>> CancelAsync(Actor actor, int id, string? reason, CancellationToken ct);
    Task<Result<ReservationDto>> CompleteAsync(Actor actor, int id, CancellationToken ct);

    /// <summary>Pending -> Confirmed by a barber (own bookings) or the admin.</summary>
    Task<Result<ReservationDto>> ConfirmAsync(Actor actor, int id, CancellationToken ct);

    /// <summary>Pending -> Cancelled by a barber or the admin, with a reason the client is told.</summary>
    Task<Result<ReservationDto>> DeclineAsync(Actor actor, int id, string reason, CancellationToken ct);
    Task<Result<ReservationDto>> MarkNoShowAsync(Actor actor, int id, CancellationToken ct);

    /// <summary>Admin only. Keeps the original price snapshot.</summary>
    Task<Result<ReservationDto>> RescheduleAsync(Actor actor, int id, RescheduleReservationRequest request, CancellationToken ct);

    /// <param name="fromDate">Salon-local first day (inclusive).</param>
    /// <param name="toDate">Salon-local last day (inclusive).</param>
    Task<ReservationSummaryDto> SummaryAsync(Actor actor, DateOnly? fromDate, DateOnly? toDate, string? employeeId, CancellationToken ct);
}
