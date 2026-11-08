using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Audit;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Audit;

/// <summary>
/// Turns the ids of audit targets into words for the audit screen ("Proizvod: Vosak"), looked up when the page is read
/// so a rename shows up in old lines too. A record that is gone reads as <see cref="AuditTargets.Removed"/>.
/// </summary>
public sealed class AuditTargetLabels(OttomanDbContext db)
{
    private static readonly CultureInfo Bs = CultureInfo.GetCultureInfo("bs-Latn-BA");

    /// <summary>Label per (controller, id); controllers without a readable name are left out.</summary>
    public async Task<Dictionary<(string Controller, string Id), string>> ResolveAsync(
        IEnumerable<(string Action, string Target)> entries, CancellationToken ct)
    {
        var wanted = entries
            .Select(e => (Controller: AuditTargets.Controller(e.Action), Id: AuditTargets.PrimaryId(e.Target)))
            .Where(e => e.Id is not null)
            .Select(e => (e.Controller, Id: e.Id!))
            .Distinct().ToList();
        var labels = new Dictionary<(string Controller, string Id), string>();

        foreach (var group in wanted.GroupBy(w => w.Controller))
        {
            var ids = group.Select(g => g.Id).ToList();
            var found = await LookUpAsync(group.Key, ids, ct);
            if (found is null) continue;
            foreach (var id in ids) labels[(group.Key, id)] = found.GetValueOrDefault(id) ?? AuditTargets.Removed;
        }
        return labels;
    }

    private static List<int> Ints(List<string> ids) =>
        [.. ids.Select(i => int.TryParse(i, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1).Where(n => n >= 0)];

    private static Dictionary<string, string> Named(IEnumerable<(int Id, string Name)> rows, string prefix) =>
        rows.ToDictionary(r => r.Id.ToString(CultureInfo.InvariantCulture), r => $"{prefix}: {r.Name}");

    /// <summary>Id to label for one controller; null when that controller has nothing a person would recognise.</summary>
    private async Task<Dictionary<string, string>?> LookUpAsync(string controller, List<string> ids, CancellationToken ct)
    {
        var ints = Ints(ids);
        switch (controller)
        {
            case "Products":
                return Named((await db.Products.AsNoTracking().Where(x => ints.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct)).Select(x => (x.Id, x.Name)), "Proizvod");
            case "Brands":
                return Named((await db.Brands.AsNoTracking().Where(x => ints.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct)).Select(x => (x.Id, x.Name)), "Brend");
            case "Categories":
                return Named((await db.Categories.AsNoTracking().Where(x => ints.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct)).Select(x => (x.Id, x.Name)), "Kategorija");
            case "Services":
                return Named((await db.Services.AsNoTracking().Where(x => ints.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct)).Select(x => (x.Id, x.Name)), "Usluga");
            case "EmployeeTitles":
                return Named((await db.EmployeeTitles.AsNoTracking().Where(x => ints.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync(ct)).Select(x => (x.Id, x.Name)), "Titula");
            case "Announcements":
                return Named((await db.Announcements.AsNoTracking().Where(x => ints.Contains(x.Id)).Select(x => new { x.Id, x.Title }).ToListAsync(ct)).Select(x => (x.Id, x.Title)), "Obavijest");
            case "ShopSections":
                return Named((await db.ShopSections.AsNoTracking().Where(x => ints.Contains(x.Id)).Select(x => new { x.Id, x.Title }).ToListAsync(ct)).Select(x => (x.Id, x.Title)), "Sekcija");
            case "Employees" or "Clients" or "Users":
                return (await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                    .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() }).ToListAsync(ct))
                    .ToDictionary(u => u.Id, u => u.Name);
            case "Orders":
                return await OrderLabelsAsync(ints, ct);
            case "Reservations":
                return await ReservationLabelsAsync(ints, ct);
            default:
                return null;
        }
    }

    private async Task<TimeZoneInfo> SalonZoneAsync(CancellationToken ct) =>
        TimeZoneInfo.FindSystemTimeZoneById(await db.SalonSettings.AsNoTracking().Select(s => s.TimeZoneId).FirstAsync(ct));

    private async Task<Dictionary<string, string>> OrderLabelsAsync(List<int> ids, CancellationToken ct)
    {
        var zone = await SalonZoneAsync(ct);
        var rows = await (from o in db.Orders.AsNoTracking()
                          where ids.Contains(o.Id)
                          join u in db.Users on o.ClientId equals u.Id
                          select new { o.Id, o.CreatedAtUtc, Name = (u.FirstName + " " + u.LastName).Trim() }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id.ToString(CultureInfo.InvariantCulture),
            r => $"Narudžba od {LocalTime.ToLocal(r.CreatedAtUtc, zone).ToString("dd.MM.yyyy.", Bs)} ({r.Name})");
    }

    private async Task<Dictionary<string, string>> ReservationLabelsAsync(List<int> ids, CancellationToken ct)
    {
        var zone = await SalonZoneAsync(ct);
        var rows = await (from r in db.Reservations.AsNoTracking()
                          where ids.Contains(r.Id)
                          join u in db.Users on r.ClientId equals u.Id
                          select new { r.Id, r.StartUtc, Name = (u.FirstName + " " + u.LastName).Trim() }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id.ToString(CultureInfo.InvariantCulture),
            r => $"Termin {LocalTime.ToLocal(r.StartUtc, zone).ToString("dd.MM.yyyy. 'u' HH:mm", Bs)} ({r.Name})");
    }
}
