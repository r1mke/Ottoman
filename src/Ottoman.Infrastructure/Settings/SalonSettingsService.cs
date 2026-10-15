using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Settings;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Settings;

public class SalonSettingsService(OttomanDbContext db) : ISalonSettingsService
{
    public async Task<SalonSettingsDto> GetAsync(CancellationToken ct) =>
        ToDto(await db.SalonSettings.AsNoTracking().SingleAsync(ct));

    public async Task<SalonSettingsDto> UpdateAsync(UpdateSalonSettingsRequest r, CancellationToken ct)
    {
        var s = await db.SalonSettings.SingleAsync(ct);
        s.Name = r.Name.Trim();
        s.Address = r.Address?.Trim();
        s.Phone = r.Phone?.Trim();
        s.TimeZoneId = r.TimeZoneId;
        s.Currency = r.Currency;
        s.OfferGridMinutes = r.OfferGridMinutes;
        s.MinLeadMinutes = r.MinLeadMinutes;
        s.CancelWindowMinutes = r.CancelWindowMinutes;
        s.LowStockDefault = r.LowStockDefault;
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    private static SalonSettingsDto ToDto(SalonSettings s) => new(
        s.Name, s.Address, s.Phone, s.TimeZoneId, s.Currency,
        s.SlotMinutes, s.OfferGridMinutes, s.MinLeadMinutes, s.CancelWindowMinutes, s.LowStockDefault);
}
