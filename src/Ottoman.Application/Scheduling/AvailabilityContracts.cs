using Ottoman.Application.Common;

namespace Ottoman.Application.Scheduling;

/// <param name="Starts">UTC instants at which the appointment can start.</param>
/// <param name="LocalTimes">The same instants as salon-local "HH:mm" labels, in the same order.</param>
/// <param name="AllTimes">True when every 5-minute start is listed, false for the curated offer.</param>
public record AvailabilityDto(
    DateOnly Date,
    int DurationMinutes,
    decimal TotalPrice,
    bool AllTimes,
    IReadOnlyList<DateTime> Starts,
    IReadOnlyList<string> LocalTimes);

/// <summary>Which days of one month still have at least one free start for the chosen services.</summary>
/// <param name="Month">The first day of the month asked about.</param>
/// <param name="AvailableDays">Salon-local dates (within the month) that can still be booked.</param>
public record MonthAvailabilityDto(DateOnly Month, IReadOnlyList<DateOnly> AvailableDays);

public interface IAvailabilityService
{
    /// <param name="month">Any date in the month; only its year and month count.</param>
    Task<Result<MonthAvailabilityDto>> GetMonthAsync(
        string employeeId, DateOnly month, IReadOnlyList<int> serviceIds, CancellationToken ct);

    /// <param name="date">The salon-local calendar date.</param>
    /// <param name="excludeReservationId">Treat this reservation as free (used when an admin moves it).</param>
    Task<Result<AvailabilityDto>> GetAsync(
        string employeeId, DateOnly date, IReadOnlyList<int> serviceIds, bool allTimes, int? excludeReservationId, CancellationToken ct);
}
