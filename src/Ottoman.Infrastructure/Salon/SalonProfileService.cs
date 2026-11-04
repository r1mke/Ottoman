using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Salon;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Images;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Salon;

public class SalonProfileService(OttomanDbContext db, ImageUrlBuilder images) : ISalonProfileService
{
    public async Task<SalonProfileDto> GetAsync(CancellationToken ct)
    {
        var s = await db.SalonSettings.AsNoTracking().SingleAsync(ct);
        var hours = await db.SalonOpeningHours.AsNoTracking().ToListAsync(ct);
        return ToDto(s, hours);
    }

    public async Task<SalonProfileDto> UpdateAsync(UpdateSalonProfileRequest r, CancellationToken ct)
    {
        var s = await db.SalonSettings.SingleAsync(ct);
        s.Name = r.Name.Trim();
        s.Description = Blank(r.Description);
        s.Address = Blank(r.Address);
        s.Phone = Blank(r.Phone);
        s.MapsUrl = Blank(r.MapsUrl);
        s.InstagramUrl = Blank(r.InstagramUrl);
        s.FacebookUrl = Blank(r.FacebookUrl);
        if (r.AutoConfirmReservations is { } auto) s.AutoConfirmReservations = auto;
        s.Latitude = r.Latitude;
        s.Longitude = r.Longitude;
        // Pasting a Google Maps link is enough: the pin comes out of the link unless coordinates were typed.
        if (s.Latitude is null && s.MapsUrl is not null
            && MapsLink.TryExtractCoordinates(s.MapsUrl, out var lat, out var lng))
        {
            s.Latitude = lat;
            s.Longitude = lng;
        }

        var hours = await db.SalonOpeningHours.ToListAsync(ct);
        foreach (var day in r.Hours)
        {
            var row = hours.FirstOrDefault(h => (int)h.Day == day.Day);
            if (row is null)
            {
                row = new SalonOpeningHours { Day = (DayOfWeek)day.Day };
                db.SalonOpeningHours.Add(row);
                hours.Add(row);
            }
            row.OpenMinutes = day.OpenMinutes;
            row.CloseMinutes = day.CloseMinutes;
        }
        await db.SaveChangesAsync(ct);
        return ToDto(s, hours);
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private SalonProfileDto ToDto(SalonSettings s, List<SalonOpeningHours> hours) => new(
        s.Name, s.Description, s.Address, s.Phone, s.MapsUrl, images.Url(s.LogoKey),
        s.Latitude, s.Longitude,
        // Monday first, the way people read a week; the day number stays 0 = Sunday.
        Enumerable.Range(1, 7).Select(i => i % 7)
            .Select(d => hours.FirstOrDefault(h => (int)h.Day == d) is { } h
                ? new OpeningHoursDto(d, h.OpenMinutes, h.CloseMinutes)
                : new OpeningHoursDto(d, null, null)).ToList(),
        s.InstagramUrl, s.FacebookUrl, s.AutoConfirmReservations);
}
