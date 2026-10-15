using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Scheduling;
using Ottoman.Domain;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Scheduling;

/// <summary>Loads the facts, then delegates every decision to the pure <see cref="SlotCalculator"/>.</summary>
public class AvailabilityService(OttomanDbContext db, TimeProvider clock) : IAvailabilityService
{
    public async Task<Result<AvailabilityDto>> GetAsync(
        string employeeId, DateOnly date, IReadOnlyList<int> serviceIds, bool allTimes, CancellationToken ct)
    {
        var ids = serviceIds.Distinct().ToList();
        if (ids.Count == 0) return Result<AvailabilityDto>.Invalid("Odaberite barem jednu uslugu.");

        var employee = await db.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == employeeId && p.IsActive, ct);
        if (employee is null) return Result<AvailabilityDto>.NotFound("Zaposlenik nije pronađen.");

        // Only active services this employee actually performs can be booked with them.
        var offered = await db.EmployeeServices.AsNoTracking()
            .Where(es => es.EmployeeId == employeeId && es.Service.IsActive)
            .Select(es => es.Service)
            .ToListAsync(ct);
        var chosen = offered.Where(s => ids.Contains(s.Id)).ToList();
        if (chosen.Count != ids.Count)
            return Result<AvailabilityDto>.Invalid("Zaposlenik ne radi jednu ili više odabranih usluga.");

        var settings = await db.SalonSettings.AsNoTracking().SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var dayRange = LocalTime.DayRangeUtc(date, zone);

        var working = await db.WorkingHours.AsNoTracking()
            .Where(w => w.EmployeeId == employeeId && w.DayOfWeek == date.DayOfWeek)
            .Select(w => new WorkingDay(w.Start, w.End, w.BreakStart, w.BreakEnd))
            .FirstOrDefaultAsync(ct);

        var reservations = await db.Reservations.AsNoTracking()
            .Where(r => r.EmployeeId == employeeId
                        && r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.NoShow
                        && r.StartUtc < dayRange.End && r.EndUtc > dayRange.Start)
            .Select(r => new TimeRange(r.StartUtc, r.EndUtc))
            .ToListAsync(ct);
        var timeOff = await db.TimeOff.AsNoTracking()
            .Where(t => t.EmployeeId == employeeId && t.StartUtc < dayRange.End && t.EndUtc > dayRange.Start)
            .Select(t => new TimeRange(t.StartUtc, t.EndUtc))
            .ToListAsync(ct);

        var duration = chosen.Sum(s => s.DurationMinutes);
        var query = new SlotQuery(
            date, zone, working, [.. reservations, .. timeOff],
            duration, settings.SlotMinutes, settings.OfferGridMinutes, settings.MinLeadMinutes,
            clock.GetUtcNow().UtcDateTime,
            ShortestServiceMinutes: offered.Min(s => s.DurationMinutes));

        var starts = allTimes ? SlotCalculator.AllStarts(query) : SlotCalculator.OfferedStarts(query);
        var labels = starts.Select(s => LocalTime.ToLocal(s, zone).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)).ToList();
        return Result<AvailabilityDto>.Ok(new AvailabilityDto(date, duration, chosen.Sum(s => s.Price), allTimes, starts, labels));
    }
}
