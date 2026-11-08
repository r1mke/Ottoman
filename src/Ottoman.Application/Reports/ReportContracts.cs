using FluentValidation;
using Ottoman.Application.Common;
using Ottoman.Domain.Reports;

namespace Ottoman.Application.Reports;

public record Kpi(decimal Value, double? ChangePercent);

public record SeriesPoint(DateOnly Start, string Label, decimal Services, decimal Products, decimal Total);

public record RankedItem(string Key, string Name, int Quantity, decimal Revenue);

public record EmployeeRow(
    string EmployeeId,
    string Name,
    int Completed,
    int Cancelled,
    int NoShow,
    decimal Revenue,
    double? AverageRating,
    int ReviewCount);

/// <summary>
/// Everything the dashboard shows for one period. Money comes from snapshots taken when the booking/order was made,
/// so changing a price later never rewrites history. Cancelled and no-show appointments earn nothing.
/// </summary>
public record DashboardDto(
    DateOnly From,
    DateOnly To,
    Granularity Granularity,
    Kpi Revenue,
    Kpi ServicesRevenue,
    Kpi ProductsRevenue,
    Kpi Appointments,
    Kpi Orders,
    Kpi NewClients,
    decimal AverageTicket,
    double NoShowRate,
    double CancellationRate,
    IReadOnlyList<SeriesPoint> Series,
    IReadOnlyList<RankedItem> TopServices,
    IReadOnlyList<RankedItem> TopProducts,
    IReadOnlyList<EmployeeRow> Employees);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ColumnType
{
    Text,
    Number,
    Money,
    Date,
    Percent,
}

public record TableColumn(string Key, string Title, ColumnType Type);

/// <summary>A report as plain rows and columns: the one shape the screens, CSV, Excel and PDF all consume.</summary>
public record TableDto(
    string Title,
    IReadOnlyList<TableColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    IReadOnlyList<object?>? Totals = null);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ExportFormat
{
    Csv,
    Xlsx,
    Pdf,
}

public record ExportedFile(byte[] Content, string ContentType, string FileName);

// ------------------------------------------------------------------ custom reports

public static class CustomReportSources
{
    public const string Reservations = "Reservations";
    public const string Orders = "Orders";
}

public record CustomColumn(string Key, string Title, ColumnType Type);

public record CustomSource(string Source, string Title, IReadOnlyList<CustomColumn> Columns, IReadOnlyList<string> Statuses);

/// <summary>
/// A report built from a fixed menu of fields and filters. Anything not on the menu is rejected: the request never
/// becomes SQL text, only choices among queries the server wrote.
/// </summary>
public record CustomReportRequest(
    string Source,
    IReadOnlyList<string> Columns,
    DateOnly From,
    DateOnly To,
    string? Status = null,
    string? EmployeeId = null);

/// <summary>The same rules the report runner applies (CustomReportCatalog.Validate), at the HTTP edge.</summary>
public class CustomReportRequestValidator : AbstractValidator<CustomReportRequest>
{
    public CustomReportRequestValidator(TimeProvider clock)
    {
        RuleFor(x => x).Custom((r, ctx) =>
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var error = r.Columns is null ? "Odaberite kolone izvještaja." : CustomReportCatalog.Validate(r, today);
            if (error is not null) ctx.AddFailure("Izvještaj", error);
        });
    }
}

public static class CustomReportCatalog
{
    public static readonly IReadOnlyList<CustomSource> Sources =
    [
        new(CustomReportSources.Reservations, "Termini",
        [
            new("date", "Datum", ColumnType.Date),
            new("time", "Vrijeme", ColumnType.Text),
            new("client", "Klijent", ColumnType.Text),
            new("employee", "Uposlenik", ColumnType.Text),
            new("services", "Usluge", ColumnType.Text),
            new("status", "Status", ColumnType.Text),
            new("duration", "Trajanje (min)", ColumnType.Number),
            new("price", "Cijena", ColumnType.Money),
        ], ["Pending", "Confirmed", "Completed", "Cancelled", "NoShow"]),
        new(CustomReportSources.Orders, "Narudžbe",
        [
            new("date", "Datum", ColumnType.Date),
            new("client", "Klijent", ColumnType.Text),
            new("items", "Proizvodi", ColumnType.Text),
            new("status", "Status", ColumnType.Text),
            new("payment", "Plaćanje", ColumnType.Text),
            new("total", "Ukupno", ColumnType.Money),
        ], ["PendingPayment", "Placed", "Fulfilled", "Cancelled", "Expired"]),
    ];

    public static CustomSource? Find(string? source) =>
        Sources.FirstOrDefault(s => string.Equals(s.Source, source, StringComparison.Ordinal));

    /// <summary>Why the request cannot be run, or null if it can.</summary>
    public static string? Validate(CustomReportRequest r, DateOnly today)
    {
        var source = Find(r.Source);
        if (source is null) return "Nepoznat izvor podataka.";
        if (r.Columns is null || r.Columns.Count == 0) return "Odaberite barem jedno polje.";
        if (r.Columns.Count != r.Columns.Distinct().Count()) return "Polja se ne smiju ponavljati.";
        var unknown = r.Columns.FirstOrDefault(c => source.Columns.All(sc => sc.Key != c));
        if (unknown is not null) return $"Nepoznato polje „{unknown}“.";
        if (r.Status is not null && !source.Statuses.Contains(r.Status)) return "Nepoznat status.";
        if (r.EmployeeId is not null && source.Source != CustomReportSources.Reservations)
            return "Filter po uposleniku važi samo za termine.";
        return ReportMath.ValidateRange(r.From, r.To, today);
    }
}

public interface IReportService
{
    Task<Result<DashboardDto>> DashboardAsync(DateOnly from, DateOnly to, CancellationToken ct);

    /// <param name="report">revenue, employees, services, products, reservations or orders.</param>
    Task<Result<TableDto>> TableAsync(string report, DateOnly from, DateOnly to, CancellationToken ct);

    Task<Result<TableDto>> CustomAsync(CustomReportRequest request, CancellationToken ct);
}

public interface IReportExporter
{
    ExportedFile Export(TableDto table, ExportFormat format);
}
