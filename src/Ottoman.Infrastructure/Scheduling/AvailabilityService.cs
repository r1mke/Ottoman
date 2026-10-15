using Ottoman.Application.Common;
using Ottoman.Application.Scheduling;
using Ottoman.Domain.Scheduling;

namespace Ottoman.Infrastructure.Scheduling;

/// <summary>Loads the facts, then delegates every decision to the pure <see cref="SlotCalculator"/>.</summary>
public class AvailabilityService(AvailabilityLoader loader) : IAvailabilityService
{
    public async Task<Result<AvailabilityDto>> GetAsync(
        string employeeId, DateOnly date, IReadOnlyList<int> serviceIds, bool allTimes, CancellationToken ct)
    {
        var loaded = await loader.LoadAsync(employeeId, date, serviceIds, excludeReservationId: null, ct);
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
