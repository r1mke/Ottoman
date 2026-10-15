using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Scheduling;

public sealed record LoadedAvailability(
    SlotQuery Query,
    IReadOnlyList<Service> Chosen,
    TimeZoneInfo Zone,
    SalonSettings Settings);

/// <summary>
/// Gathers the facts needed to compute free starts (working hours, bookings, time off, chosen services).
/// Shared by the availability endpoint and by reservation creation, so a booking is validated against
/// exactly what the client was shown.
/// </summary>
public class AvailabilityLoader(OttomanDbContext db, TimeProvider clock)
{
    public async Task<Result<LoadedAvailability>> LoadAsync(
        string employeeId,
        DateOnly date,
        IReadOnlyList<int> serviceIds,
        int? excludeReservationId,
        CancellationToken ct)
    {
        var ids = serviceIds.Distinct().ToList();
        if (ids.Count == 0) return Result<LoadedAvailability>.Invalid("Odaberite barem jednu uslugu.");

        var employee = await db.EmployeeProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == employeeId && p.IsActive, ct);
        if (employee is null) return Result<LoadedAvailability>.NotFound("Zaposlenik nije pronađen.");

        // Only active services this employee actually performs can be booked with them.
        var offered = await db.EmployeeServices.AsNoTracking()
            .Where(es => es.EmployeeId == employeeId && es.Service.IsActive)
            .Select(es => es.Service)
            .ToListAsync(ct);
        var chosen = offered.Where(s => ids.Contains(s.Id)).ToList();
        if (chosen.Count != ids.Count)
            return Result<LoadedAvailability>.Invalid("Zaposlenik ne radi jednu ili više odabranih usluga.");

        var settings = await db.SalonSettings.AsNoTracking().SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var dayRange = LocalTime.DayRangeUtc(date, zone);

        var working = await db.WorkingHours.AsNoTracking()
            .Where(w => w.EmployeeId == employeeId && w.DayOfWeek == date.DayOfWeek)
            .Select(w => new WorkingDay(w.Start, w.End, w.BreakStart, w.BreakEnd))
            .FirstOrDefaultAsync(ct);

        var reservations = await db.Reservations.AsNoTracking()
            .Where(r => r.EmployeeId == employeeId
                        && r.Id != excludeReservationId
                        && r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.NoShow
                        && r.StartUtc < dayRange.End && r.EndUtc > dayRange.Start)
            .Select(r => new TimeRange(r.StartUtc, r.EndUtc))
            .ToListAsync(ct);
        var timeOff = await db.TimeOff.AsNoTracking()
            .Where(t => t.EmployeeId == employeeId && t.StartUtc < dayRange.End && t.EndUtc > dayRange.Start)
            .Select(t => new TimeRange(t.StartUtc, t.EndUtc))
            .ToListAsync(ct);

        var query = new SlotQuery(
            date, zone, working, [.. reservations, .. timeOff],
            chosen.Sum(s => s.DurationMinutes), settings.SlotMinutes, settings.OfferGridMinutes, settings.MinLeadMinutes,
            clock.GetUtcNow().UtcDateTime,
            ShortestServiceMinutes: offered.Min(s => s.DurationMinutes));

        return Result<LoadedAvailability>.Ok(new LoadedAvailability(query, chosen, zone, settings));
    }
}
