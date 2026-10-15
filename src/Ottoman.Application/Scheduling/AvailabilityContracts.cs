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

public interface IAvailabilityService
{
    /// <param name="date">The salon-local calendar date.</param>
    Task<Result<AvailabilityDto>> GetAsync(
        string employeeId, DateOnly date, IReadOnlyList<int> serviceIds, bool allTimes, CancellationToken ct);
}
