using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Reports;

namespace Ottoman.Api.Controllers;

/// <summary>
/// Reports and the dashboard. Admin only: earnings are the owner's business, employees never see them.
/// Dates are salon-local calendar days, both ends inclusive.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = Policies.AdminOnly)]
public class ReportsController(IReportService reports, IReportExporter exporter) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> Dashboard([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        this.ToActionResult(await reports.DashboardAsync(from, to, ct));

    /// <param name="report">revenue, employees, services, products, reservations, orders</param>
    [HttpGet("{report}")]
    public async Task<ActionResult<TableDto>> Table(string report, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        this.ToActionResult(await reports.TableAsync(report, from, to, ct));

    [HttpGet("{report}/export")]
    public async Task<IActionResult> Export(
        string report, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] ExportFormat format = ExportFormat.Xlsx, CancellationToken ct = default)
    {
        var table = await reports.TableAsync(report, from, to, ct);
        if (!table.Succeeded) return this.ToActionResult(table).Result!;
        var file = exporter.Export(table.Value!, format);
        return File(file.Content, file.ContentType, file.FileName);
    }

    // ---------------------------------------------------------------- custom report builder

    /// <summary>The menu the report builder offers: sources, their fields and statuses.</summary>
    [HttpGet("custom/fields")]
    public IReadOnlyList<CustomSource> Fields() => CustomReportCatalog.Sources;

    [HttpPost("custom")]
    public async Task<ActionResult<TableDto>> Custom(CustomReportRequest request, CancellationToken ct) =>
        this.ToActionResult(await reports.CustomAsync(request, ct));

    [HttpPost("custom/export")]
    public async Task<IActionResult> CustomExport(CustomReportRequest request, [FromQuery] ExportFormat format = ExportFormat.Xlsx, CancellationToken ct = default)
    {
        var table = await reports.CustomAsync(request, ct);
        if (!table.Succeeded) return this.ToActionResult(table).Result!;
        var file = exporter.Export(table.Value!, format);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
