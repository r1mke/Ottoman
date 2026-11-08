using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Reports;
using Ottoman.Domain;
using Ottoman.Domain.Reports;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Reports;

/// <summary>
/// Read-only aggregation over the snapshots stored with every reservation and order. Rows are fetched with only the
/// columns needed and bucketed in memory by salon-local date, which keeps daylight-saving days correct; for one salon
/// the volume is small.
/// </summary>
public class ReportService(OttomanDbContext db, TimeProvider clock) : IReportService
{
    private const int MaxRows = 10_000;

    private sealed record Period(
        decimal ServicesRevenue,
        decimal ProductsRevenue,
        int Completed,
        int Cancelled,
        int NoShow,
        int Orders,
        int FulfilledOrders,
        int NewClients,
        List<(DateTime At, decimal Amount)> ServiceSales,
        List<(DateTime At, decimal Amount)> ProductSales)
    {
        public decimal Revenue => ServicesRevenue + ProductsRevenue;
    }

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    // ---------------------------------------------------------------- dashboard

    public async Task<Result<DashboardDto>> DashboardAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var problem = ReportMath.ValidateRange(from, to, Today);
        if (problem is not null) return Result<DashboardDto>.Invalid(problem);

        var zone = await ZoneAsync(ct);
        var (prevFrom, prevTo) = ReportMath.PreviousPeriod(from, to);
        var current = await LoadPeriodAsync(from, to, zone, ct);
        var previous = await LoadPeriodAsync(prevFrom, prevTo, zone, ct);

        var granularity = ReportMath.AutoGranularity(from, to);
        var series = Series(current, from, to, granularity, zone);

        var closed = current.Completed + current.Cancelled + current.NoShow;
        var sales = current.Completed + current.FulfilledOrders;

        var dto = new DashboardDto(
            from, to, granularity,
            new Kpi(current.Revenue, ReportMath.PercentChange(current.Revenue, previous.Revenue)),
            new Kpi(current.ServicesRevenue, ReportMath.PercentChange(current.ServicesRevenue, previous.ServicesRevenue)),
            new Kpi(current.ProductsRevenue, ReportMath.PercentChange(current.ProductsRevenue, previous.ProductsRevenue)),
            new Kpi(current.Completed, ReportMath.PercentChange(current.Completed, previous.Completed)),
            new Kpi(current.Orders, ReportMath.PercentChange(current.Orders, previous.Orders)),
            new Kpi(current.NewClients, ReportMath.PercentChange(current.NewClients, previous.NewClients)),
            ReportMath.Average(current.Revenue, sales),
            ReportMath.Rate(current.NoShow, closed),
            ReportMath.Rate(current.Cancelled, closed),
            series,
            await TopServicesAsync(from, to, zone, 5, ct),
            await TopProductsAsync(from, to, zone, 5, ct),
            await EmployeeRowsAsync(from, to, zone, ct));
        return Result<DashboardDto>.Ok(dto);
    }

    private async Task<Period> LoadPeriodAsync(DateOnly from, DateOnly to, TimeZoneInfo zone, CancellationToken ct)
    {
        var (a, b) = Range(from, to, zone);

        var reservations = await db.Reservations.AsNoTracking()
            .Where(r => r.StartUtc >= a && r.StartUtc < b)
            .Select(r => new { r.Status, r.StartUtc, r.TotalPrice }).ToListAsync(ct);
        var serviceSales = reservations.Where(r => r.Status == ReservationStatus.Completed)
            .Select(r => (r.StartUtc, r.TotalPrice)).ToList();

        var fulfilled = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Fulfilled && o.FulfilledAtUtc >= a && o.FulfilledAtUtc < b)
            .Select(o => new { At = o.FulfilledAtUtc!.Value, o.TotalPrice }).ToListAsync(ct);
        var productSales = fulfilled.Select(o => (o.At, o.TotalPrice)).ToList();

        var orders = await db.Orders.CountAsync(
            o => (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Fulfilled) && o.CreatedAtUtc >= a && o.CreatedAtUtc < b, ct);

        var clientRoleId = await db.Roles.Where(r => r.Name == Roles.Client).Select(r => r.Id).SingleAsync(ct);
        var newClients = await (
            from ur in db.UserRoles
            join u in db.Users on ur.UserId equals u.Id
            where ur.RoleId == clientRoleId && u.RegisteredAtUtc >= a && u.RegisteredAtUtc < b
            select u.Id).CountAsync(ct);

        return new Period(
            serviceSales.Sum(s => s.TotalPrice),
            productSales.Sum(s => s.TotalPrice),
            serviceSales.Count,
            reservations.Count(r => r.Status == ReservationStatus.Cancelled),
            reservations.Count(r => r.Status == ReservationStatus.NoShow),
            orders,
            fulfilled.Count,
            newClients,
            serviceSales,
            productSales);
    }

    private static List<SeriesPoint> Series(Period p, DateOnly from, DateOnly to, Granularity g, TimeZoneInfo zone)
    {
        DateOnly Bucket(DateTime utc) => ReportMath.BucketStart(DateOnly.FromDateTime(LocalTime.ToLocal(utc, zone)), g);

        var services = p.ServiceSales.GroupBy(s => Bucket(s.At)).ToDictionary(x => x.Key, x => x.Sum(s => s.Amount));
        var products = p.ProductSales.GroupBy(s => Bucket(s.At)).ToDictionary(x => x.Key, x => x.Sum(s => s.Amount));

        return ReportMath.Buckets(from, to, g).Select(b =>
        {
            var s = services.GetValueOrDefault(b);
            var pr = products.GetValueOrDefault(b);
            return new SeriesPoint(b, ReportMath.Label(b, g), s, pr, s + pr);
        }).ToList();
    }

    private async Task<List<RankedItem>> TopServicesAsync(DateOnly from, DateOnly to, TimeZoneInfo zone, int take, CancellationToken ct)
    {
        var (a, b) = Range(from, to, zone);
        var rows = await db.ReservationItems.AsNoTracking()
            .Where(i => db.Reservations.Any(r => r.Id == i.ReservationId && r.Status == ReservationStatus.Completed && r.StartUtc >= a && r.StartUtc < b))
            .GroupBy(i => i.ServiceId)
            .Select(g => new { Id = g.Key, Name = g.Max(i => i.ServiceName), Qty = g.Count(), Revenue = g.Sum(i => i.Price) })
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.Revenue).ThenBy(r => r.Name).Take(take)
            .Select(r => new RankedItem(r.Id.ToString(), r.Name ?? "?", r.Qty, r.Revenue)).ToList();
    }

    private async Task<List<RankedItem>> TopProductsAsync(DateOnly from, DateOnly to, TimeZoneInfo zone, int take, CancellationToken ct)
    {
        var (a, b) = Range(from, to, zone);
        var rows = await db.OrderItems.AsNoTracking()
            .Where(i => db.Orders.Any(o => o.Id == i.OrderId && o.Status == OrderStatus.Fulfilled && o.FulfilledAtUtc >= a && o.FulfilledAtUtc < b))
            .GroupBy(i => i.ProductId)
            .Select(g => new { Id = g.Key, Name = g.Max(i => i.ProductName), Qty = g.Sum(i => i.Quantity), Revenue = g.Sum(i => i.UnitPrice * i.Quantity) })
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.Revenue).ThenBy(r => r.Name).Take(take)
            .Select(r => new RankedItem(r.Id.ToString(), r.Name ?? "?", r.Qty, r.Revenue)).ToList();
    }

    private async Task<List<EmployeeRow>> EmployeeRowsAsync(DateOnly from, DateOnly to, TimeZoneInfo zone, CancellationToken ct)
    {
        var (a, b) = Range(from, to, zone);
        var counts = await db.Reservations.AsNoTracking()
            .Where(r => r.StartUtc >= a && r.StartUtc < b)
            .GroupBy(r => r.EmployeeId)
            .Select(g => new
            {
                Id = g.Key,
                Completed = g.Count(r => r.Status == ReservationStatus.Completed),
                Cancelled = g.Count(r => r.Status == ReservationStatus.Cancelled),
                NoShow = g.Count(r => r.Status == ReservationStatus.NoShow),
                Revenue = g.Where(r => r.Status == ReservationStatus.Completed).Sum(r => (decimal?)r.TotalPrice) ?? 0m,
            }).ToDictionaryAsync(x => x.Id, ct);

        var countIds = counts.Keys.ToList();
        var employees = await (
            from p in db.EmployeeProfiles.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
            where p.IsActive || countIds.Contains(p.UserId)
            select new { p.UserId, u.FirstName, u.LastName }).ToListAsync(ct);

        var ratings = await db.Reviews.AsNoTracking()
            .Where(r => r.TargetType == ReviewTargetType.Employee && !r.IsHidden)
            .GroupBy(r => r.TargetKey).Select(g => new { g.Key, Avg = g.Average(r => (double)r.Rating), N = g.Count() })
            .ToDictionaryAsync(x => x.Key, ct);

        return employees
            .Select(e =>
            {
                counts.TryGetValue(e.UserId, out var c);
                ratings.TryGetValue(e.UserId, out var rt);
                return new EmployeeRow(
                    e.UserId, $"{e.FirstName} {e.LastName}".Trim(),
                    c?.Completed ?? 0, c?.Cancelled ?? 0, c?.NoShow ?? 0, c?.Revenue ?? 0,
                    rt is null ? null : Math.Round(rt.Avg, 2), rt?.N ?? 0);
            })
            .OrderByDescending(e => e.Revenue).ThenBy(e => e.Name).ToList();
    }

    // ---------------------------------------------------------------- tables

    public async Task<Result<TableDto>> TableAsync(string report, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var problem = ReportMath.ValidateRange(from, to, Today);
        if (problem is not null) return Result<TableDto>.Invalid(problem);
        var zone = await ZoneAsync(ct);

        switch (report)
        {
            case "revenue":
                {
                    var period = await LoadPeriodAsync(from, to, zone, ct);
                    var g = ReportMath.AutoGranularity(from, to);
                    var rows = Series(period, from, to, g, zone);
                    return Result<TableDto>.Ok(new TableDto(
                        $"Prihod {from:dd.MM.yyyy.} – {to:dd.MM.yyyy.}",
                        [new("period", g == Granularity.Month ? "Mjesec" : g == Granularity.Week ? "Sedmica od" : "Dan", ColumnType.Text),
                     new("services", "Usluge", ColumnType.Money), new("products", "Proizvodi", ColumnType.Money), new("total", "Ukupno", ColumnType.Money)],
                        rows.Select(r => (IReadOnlyList<object?>)[r.Label, r.Services, r.Products, r.Total]).ToList(),
                        ["Ukupno", period.ServicesRevenue, period.ProductsRevenue, period.Revenue]));
                }
            case "employees":
                {
                    var rows = await EmployeeRowsAsync(from, to, zone, ct);
                    return Result<TableDto>.Ok(new TableDto(
                        $"Učinak uposlenika {from:dd.MM.yyyy.} – {to:dd.MM.yyyy.}",
                        [new("name", "Uposlenik", ColumnType.Text), new("completed", "Završeno", ColumnType.Number), new("cancelled", "Otkazano", ColumnType.Number),
                     new("noshow", "Nije došao", ColumnType.Number), new("revenue", "Prihod", ColumnType.Money), new("rating", "Ocjena", ColumnType.Number), new("reviews", "Recenzija", ColumnType.Number)],
                        rows.Select(r => (IReadOnlyList<object?>)[r.Name, r.Completed, r.Cancelled, r.NoShow, r.Revenue, r.AverageRating, r.ReviewCount]).ToList(),
                        ["Ukupno", rows.Sum(r => r.Completed), rows.Sum(r => r.Cancelled), rows.Sum(r => r.NoShow), rows.Sum(r => r.Revenue), null, rows.Sum(r => r.ReviewCount)]));
                }
            case "services":
                {
                    var rows = await TopServicesAsync(from, to, zone, int.MaxValue, ct);
                    return Result<TableDto>.Ok(RankedTable($"Usluge {from:dd.MM.yyyy.} – {to:dd.MM.yyyy.}", "Usluga", rows));
                }
            case "products":
                {
                    var rows = await TopProductsAsync(from, to, zone, int.MaxValue, ct);
                    return Result<TableDto>.Ok(RankedTable($"Proizvodi {from:dd.MM.yyyy.} – {to:dd.MM.yyyy.}", "Proizvod", rows));
                }
            case "reservations":
                return await CustomAsync(new CustomReportRequest(CustomReportSources.Reservations,
                    CustomReportCatalog.Find(CustomReportSources.Reservations)!.Columns.Select(c => c.Key).ToList(), from, to), ct);
            case "orders":
                return await CustomAsync(new CustomReportRequest(CustomReportSources.Orders,
                    CustomReportCatalog.Find(CustomReportSources.Orders)!.Columns.Select(c => c.Key).ToList(), from, to), ct);
            default:
                return Result<TableDto>.NotFound("Nepoznat izvještaj.");
        }
    }

    private static TableDto RankedTable(string title, string nameTitle, List<RankedItem> rows) => new(
        title,
        [new("name", nameTitle, ColumnType.Text), new("quantity", "Količina", ColumnType.Number), new("revenue", "Prihod", ColumnType.Money)],
        rows.Select(r => (IReadOnlyList<object?>)[r.Name, r.Quantity, r.Revenue]).ToList(),
        ["Ukupno", rows.Sum(r => r.Quantity), rows.Sum(r => r.Revenue)]);

    // ---------------------------------------------------------------- custom

    public async Task<Result<TableDto>> CustomAsync(CustomReportRequest r, CancellationToken ct)
    {
        var problem = CustomReportCatalog.Validate(r, Today);
        if (problem is not null) return Result<TableDto>.Invalid(problem);

        var zone = await ZoneAsync(ct);
        var source = CustomReportCatalog.Find(r.Source)!;
        var columns = r.Columns.Select(key => source.Columns.Single(c => c.Key == key)).ToList();

        var rows = r.Source == CustomReportSources.Reservations
            ? await ReservationRowsAsync(r, zone, ct)
            : await OrderRowsAsync(r, zone, ct);
        if (rows is null) return Result<TableDto>.Invalid($"Previše redova (više od {MaxRows}). Suzite period ili filtere.");

        var table = rows.Select(row => (IReadOnlyList<object?>)columns.Select(c => row.GetValueOrDefault(c.Key)).ToList()).ToList();

        // A total only where it makes sense: the money column, when one was chosen.
        var money = columns.FindIndex(c => c.Type == ColumnType.Money);
        IReadOnlyList<object?>? totals = null;
        if (money >= 0)
        {
            var t = new object?[columns.Count];
            t[0] = money == 0 ? null : "Ukupno";
            t[money] = table.Sum(row => row[money] as decimal? ?? 0m);
            totals = t;
        }

        return Result<TableDto>.Ok(new TableDto($"{source.Title} {r.From:dd.MM.yyyy.} – {r.To:dd.MM.yyyy.}", columns.Select(c => new TableColumn(c.Key, c.Title, c.Type)).ToList(), table, totals));
    }

    private async Task<List<Dictionary<string, object?>>?> ReservationRowsAsync(CustomReportRequest r, TimeZoneInfo zone, CancellationToken ct)
    {
        var (a, b) = Range(r.From, r.To, zone);
        var query = db.Reservations.AsNoTracking().Where(x => x.StartUtc >= a && x.StartUtc < b);
        if (r.Status is not null) query = query.Where(x => x.Status == Enum.Parse<ReservationStatus>(r.Status));
        if (r.EmployeeId is not null) query = query.Where(x => x.EmployeeId == r.EmployeeId);

        if (await query.CountAsync(ct) > MaxRows) return null;
        var list = await query.OrderBy(x => x.StartUtc).Select(x => new
        {
            x.StartUtc,
            x.ClientId,
            x.EmployeeId,
            x.Status,
            x.TotalDurationMinutes,
            x.TotalPrice,
            Services = x.Items.Select(i => i.ServiceName).ToList(),
        }).ToListAsync(ct);

        var names = await NamesAsync(list.Select(x => x.ClientId).Concat(list.Select(x => x.EmployeeId)).Distinct().ToList(), ct);
        return list.Select(x =>
        {
            var local = LocalTime.ToLocal(x.StartUtc, zone);
            return new Dictionary<string, object?>
            {
                ["date"] = local.ToString("yyyy-MM-dd"),
                ["time"] = local.ToString("HH:mm"),
                ["client"] = names.GetValueOrDefault(x.ClientId, "?"),
                ["employee"] = names.GetValueOrDefault(x.EmployeeId, "?"),
                ["services"] = string.Join(", ", x.Services),
                ["status"] = x.Status.ToString(),
                ["duration"] = x.TotalDurationMinutes,
                ["price"] = x.TotalPrice,
            };
        }).ToList();
    }

    private async Task<List<Dictionary<string, object?>>?> OrderRowsAsync(CustomReportRequest r, TimeZoneInfo zone, CancellationToken ct)
    {
        var (a, b) = Range(r.From, r.To, zone);
        var query = db.Orders.AsNoTracking().Where(x => x.CreatedAtUtc >= a && x.CreatedAtUtc < b);
        if (r.Status is not null) query = query.Where(x => x.Status == Enum.Parse<OrderStatus>(r.Status));

        if (await query.CountAsync(ct) > MaxRows) return null;
        var list = await query.OrderBy(x => x.CreatedAtUtc).Select(x => new
        {
            x.CreatedAtUtc,
            x.ClientId,
            x.Status,
            x.PaymentMethod,
            x.TotalPrice,
            Items = x.Items.Select(i => new { i.Quantity, i.ProductName }).ToList(),
        }).ToListAsync(ct);

        var names = await NamesAsync(list.Select(x => x.ClientId).Distinct().ToList(), ct);
        return list.Select(x => new Dictionary<string, object?>
        {
            ["date"] = LocalTime.ToLocal(x.CreatedAtUtc, zone).ToString("yyyy-MM-dd"),
            ["client"] = names.GetValueOrDefault(x.ClientId, "?"),
            ["items"] = string.Join(", ", x.Items.Select(i => $"{i.Quantity}× {i.ProductName}")),
            ["status"] = x.Status.ToString(),
            ["payment"] = x.PaymentMethod.ToString(),
            ["total"] = x.TotalPrice,
        }).ToList();
    }

    // ---------------------------------------------------------------- helpers

    private async Task<TimeZoneInfo> ZoneAsync(CancellationToken ct) =>
        TimeZoneInfo.FindSystemTimeZoneById(await db.SalonSettings.AsNoTracking().Select(s => s.TimeZoneId).FirstAsync(ct));

    /// <summary>UTC range [start of <paramref name="from"/>, start of day after <paramref name="to"/>).</summary>
    private static (DateTime A, DateTime B) Range(DateOnly from, DateOnly to, TimeZoneInfo zone) =>
        (LocalTime.ToUtc(from, TimeOnly.MinValue, zone), LocalTime.ToUtc(to.AddDays(1), TimeOnly.MinValue, zone));

    private async Task<Dictionary<string, string>> NamesAsync(List<string> ids, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);
}
