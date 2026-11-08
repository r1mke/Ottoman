using Ottoman.Application.Common;
using Ottoman.Application.Scheduling;
using Ottoman.Domain.Reservations;
using Ottoman.Domain.Scheduling;

namespace Ottoman.Infrastructure.Scheduling;

/// <summary>Loads the facts, then delegates every decision to the pure <see cref="SlotCalculator"/>.</summary>
public class AvailabilityService(AvailabilityLoader loader, TimeProvider clock) : IAvailabilityService
{
    public async Task<Result<MonthAvailabilityDto>> GetMonthAsync(
        string employeeId, DateOnly month, IReadOnlyList<int> serviceIds, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        // A missing or absurd month (0001-01, 9999-12) is refused before any date arithmetic can overflow.
        if (!BookingPolicy.MonthInDateWindow(month, today))
            return Result<MonthAvailabilityDto>.Invalid(BookingPolicy.OutsideWindowMessage);
        var first = new DateOnly(month.Year, month.Month, 1);
        var window = BookingPolicy.DateWindow(today);

        var open = new List<DateOnly>();
        var last = first.AddMonths(1).AddDays(-1);
        var from = first < today ? today : first;
        var until = last > window.Last ? window.Last : last;
        // Each day goes through the same rules as the single-day endpoint, so the calendar never promises a time
        // that the day itself would not offer.
        for (var day = from; day <= until; day = day.AddDays(1))
        {
            var one = await GetAsync(employeeId, day, serviceIds, false, null, ct);
            if (!one.Succeeded) return new Result<MonthAvailabilityDto>(one.Status, Error: one.Error);
            if (one.Value!.Starts.Count > 0) open.Add(day);
        }
        return Result<MonthAvailabilityDto>.Ok(new MonthAvailabilityDto(first, open));
    }

    public async Task<Result<AvailabilityDto>> GetAsync(
        string employeeId, DateOnly date, IReadOnlyList<int> serviceIds, bool allTimes, int? excludeReservationId, CancellationToken ct)
    {
        if (!BookingPolicy.InDateWindow(date, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)))
            return Result<AvailabilityDto>.Invalid(BookingPolicy.OutsideWindowMessage);
        var loaded = await loader.LoadAsync(employeeId, date, serviceIds, excludeReservationId, ct);
        if (!loaded.Succeeded) return new Result<AvailabilityDto>(loaded.Status, Error: loaded.Error);

        var (query, chosen, zone, _) = loaded.Value!;
        var starts = allTimes ? SlotCalculator.AllStarts(query) : SlotCalculator.OfferedStarts(query);
        var labels = starts
            .Select(s => LocalTime.ToLocal(s, zone).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        return Result<AvailabilityDto>.Ok(new AvailabilityDto(
            date, query.DurationMinutes, chosen.Sum(s => s.Price), allTimes, starts, labels));
    }
}
