using System.Net;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Reports;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

/// <summary>
/// Reports are checked against rows inserted directly with known dates and amounts. Every test uses its own window in
/// 2037, which no other test touches, so the expected numbers are exact.
/// </summary>
[Collection("api")]
public class ReportTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, HttpClient Http)> NewClient()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<string> KerimId() =>
        (await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword)).User.Id;

    private async Task<string> TarikId() =>
        (await api.CreateClient().LoginOk("tarik@ottoman.local", ApiFixture.DemoPassword)).User.Id;

    private static string Q(DateOnly from, DateOnly to) => $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

    private async Task Reservation(string clientId, string employeeId, DateTime startUtc, decimal price, ReservationStatus status, string serviceName = "Šišanje", int serviceId = 0)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        // the snapshot name is what reports show, but the foreign key needs a real service
        var realServiceId = await db.Services.Where(s => s.Name == serviceName).Select(s => s.Id).FirstAsync();
        db.Reservations.Add(new Reservation
        {
            ClientId = clientId,
            EmployeeId = employeeId,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(30),
            Status = status,
            TotalPrice = price,
            TotalDurationMinutes = 30,
            CreatedAtUtc = startUtc.AddDays(-2),
            Items = [new ReservationItem { ServiceId = realServiceId, ServiceName = serviceName, Price = price, DurationMinutes = 30 }],
        });
        await db.SaveChangesAsync();
    }

    private async Task Order(string clientId, DateTime at, decimal total, OrderStatus status, string product = "Vosak", int productId = 1, int qty = 1)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var realProductId = await db.Products.Select(p => p.Id).FirstAsync();
        db.Orders.Add(new Order
        {
            ClientId = clientId,
            Status = status,
            Fulfilment = FulfilmentType.Pickup,
            PaymentMethod = PaymentMethod.OnReceipt,
            TotalPrice = total,
            CreatedAtUtc = at.AddHours(-1),
            FulfilledAtUtc = status == OrderStatus.Fulfilled ? at : null,
            Items = [new OrderItem { ProductId = realProductId, ProductName = product, UnitPrice = total / qty, Quantity = qty }],
        });
        await db.SaveChangesAsync();
    }

    private static DateTime At(int y, int m, int d, int h = 10) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    // ------------------------------------------------------------------ access

    [Fact]
    public async Task Reports_are_for_admins_only_so_staff_never_see_earnings()
    {
        var from = new DateOnly(2037, 1, 1);
        var (_, client) = await NewClient();
        var barber = api.CreateClient().As(await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword));

        foreach (var url in new[]
                 {
                     $"/api/v1/reports/dashboard?{Q(from, from)}", $"/api/v1/reports/revenue?{Q(from, from)}",
                     $"/api/v1/reports/employees?{Q(from, from)}", $"/api/v1/reports/revenue/export?{Q(from, from)}&format=csv",
                     "/api/v1/reports/custom/fields",
                 })
        {
            (await api.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, url);
            (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden, url);
            (await barber.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden, url);
        }
        var body = new CustomReportRequest("Reservations", ["date"], from, from);
        (await client.PostAsJsonAsync("/api/v1/reports/custom", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await barber.PostAsJsonAsync("/api/v1/reports/custom", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Bad_ranges_are_refused()
    {
        var admin = await Admin();
        (await admin.GetAsync("/api/v1/reports/dashboard?from=2037-02-10&to=2037-02-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/reports/dashboard?from=2030-01-01&to=2037-02-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/v1/reports/nonsense?from=2037-02-01&to=2037-02-02")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------ the numbers

    [Fact]
    public async Task Revenue_counts_only_completed_appointments_and_fulfilled_orders_and_compares_with_the_period_before()
    {
        var (clientId, _) = await NewClient();
        var kerim = await KerimId();
        // window: 1-10 March 2037 (10 days); previous period: 19-28 February 2037
        await Reservation(clientId, kerim, At(2037, 3, 2), 25, ReservationStatus.Completed);
        await Reservation(clientId, kerim, At(2037, 3, 5), 40, ReservationStatus.Completed);
        await Reservation(clientId, kerim, At(2037, 3, 6), 100, ReservationStatus.Cancelled);
        await Reservation(clientId, kerim, At(2037, 3, 7), 30, ReservationStatus.NoShow);
        await Reservation(clientId, kerim, At(2037, 3, 8), 999, ReservationStatus.Confirmed);   // not done yet: no money
        await Order(clientId, At(2037, 3, 4), 20, OrderStatus.Fulfilled);
        await Order(clientId, At(2037, 3, 4), 500, OrderStatus.Cancelled);
        await Order(clientId, At(2037, 3, 4), 500, OrderStatus.Placed);                          // paid/reserved but not handed over
        await Reservation(clientId, kerim, At(2037, 2, 25), 50, ReservationStatus.Completed);   // previous period

        var d = (await (await Admin()).GetFromJsonAsync<DashboardDto>("/api/v1/reports/dashboard?from=2037-03-01&to=2037-03-10"))!;

        d.ServicesRevenue.Value.Should().Be(65m);
        d.ProductsRevenue.Value.Should().Be(20m);
        d.Revenue.Value.Should().Be(85m);
        d.Appointments.Value.Should().Be(2);
        d.ServicesRevenue.ChangePercent.Should().Be(30.0, "65 vs 50");
        d.Revenue.ChangePercent.Should().Be(70.0, "85 vs 50");
        d.AverageTicket.Should().Be(28.33m, "85 over 2 appointments and 1 order");
        d.CancellationRate.Should().Be(25.0, "1 of 4 closed (2 done, 1 cancelled, 1 no-show)");
        d.NoShowRate.Should().Be(25.0);
        d.Orders.Value.Should().Be(2, "fulfilled and placed orders were made; the cancelled one was not");
    }

    [Fact]
    public async Task The_series_has_a_bar_for_every_day_and_adds_up_to_the_total()
    {
        var (clientId, _) = await NewClient();
        var kerim = await KerimId();
        await Reservation(clientId, kerim, At(2037, 4, 2), 25, ReservationStatus.Completed);
        await Reservation(clientId, kerim, At(2037, 4, 2, 14), 15, ReservationStatus.Completed);
        await Order(clientId, At(2037, 4, 9), 12, OrderStatus.Fulfilled);

        var d = (await (await Admin()).GetFromJsonAsync<DashboardDto>("/api/v1/reports/dashboard?from=2037-04-01&to=2037-04-14"))!;

        d.Granularity.Should().Be(Ottoman.Domain.Reports.Granularity.Day);
        d.Series.Should().HaveCount(14, "empty days are included so the chart has no gaps");
        d.Series.Single(s => s.Start == new DateOnly(2037, 4, 2)).Services.Should().Be(40m);
        d.Series.Single(s => s.Start == new DateOnly(2037, 4, 9)).Products.Should().Be(12m);
        d.Series.Sum(s => s.Total).Should().Be(d.Revenue.Value);
    }

    [Fact]
    public async Task A_late_evening_visit_is_counted_on_the_salons_local_day_not_the_utc_day()
    {
        var (clientId, _) = await NewClient();
        var kerim = await KerimId();
        // 22:30 UTC on 15 June = 00:30 on 16 June in Sarajevo (UTC+2)
        await Reservation(clientId, kerim, new DateTime(2037, 6, 15, 22, 30, 0, DateTimeKind.Utc), 33, ReservationStatus.Completed);

        var d = (await (await Admin()).GetFromJsonAsync<DashboardDto>("/api/v1/reports/dashboard?from=2037-06-14&to=2037-06-17"))!;

        d.Series.Single(s => s.Start == new DateOnly(2037, 6, 16)).Services.Should().Be(33m);
        d.Series.Single(s => s.Start == new DateOnly(2037, 6, 15)).Services.Should().Be(0m);
    }

    [Fact]
    public async Task A_long_period_is_grouped_by_week_and_a_very_long_one_by_month()
    {
        var admin = await Admin();
        var weekly = (await admin.GetFromJsonAsync<DashboardDto>("/api/v1/reports/dashboard?from=2037-07-01&to=2037-10-31"))!;
        weekly.Granularity.Should().Be(Ottoman.Domain.Reports.Granularity.Week);
        weekly.Series.Should().OnlyContain(s => s.Start.DayOfWeek == DayOfWeek.Monday);

        var monthly = (await admin.GetFromJsonAsync<DashboardDto>("/api/v1/reports/dashboard?from=2037-01-01&to=2037-12-31"))!;
        monthly.Granularity.Should().Be(Ottoman.Domain.Reports.Granularity.Month);
        monthly.Series.Should().HaveCount(12);
    }

    [Fact]
    public async Task Employee_performance_and_top_lists_come_from_the_snapshots()
    {
        var (clientId, _) = await NewClient();
        var kerim = await KerimId();
        var tarik = await TarikId();
        await Reservation(clientId, kerim, At(2037, 8, 3), 25, ReservationStatus.Completed, "Šišanje");
        await Reservation(clientId, kerim, At(2037, 8, 4), 25, ReservationStatus.Completed, "Šišanje");
        await Reservation(clientId, kerim, At(2037, 8, 5), 40, ReservationStatus.Completed, "Šišanje & brada");
        await Reservation(clientId, tarik, At(2037, 8, 6), 15, ReservationStatus.Completed, "Uređivanje brade");
        await Reservation(clientId, tarik, At(2037, 8, 7), 15, ReservationStatus.NoShow, "Uređivanje brade");
        await Order(clientId, At(2037, 8, 8), 30, OrderStatus.Fulfilled, "Vosak", qty: 3);

        var d = (await (await Admin()).GetFromJsonAsync<DashboardDto>("/api/v1/reports/dashboard?from=2037-08-01&to=2037-08-31"))!;

        var k = d.Employees.Single(e => e.EmployeeId == kerim);
        (k.Completed, k.Revenue).Should().Be((3, 90m));
        var t = d.Employees.Single(e => e.EmployeeId == tarik);
        (t.Completed, t.NoShow, t.Revenue).Should().Be((1, 1, 15m));
        d.Employees.First().EmployeeId.Should().Be(kerim, "ordered by revenue");

        d.TopServices.Select(s => s.Name).Should().Equal("Šišanje", "Šišanje & brada", "Uređivanje brade"); // by revenue: 50, 40, 15
        d.TopServices.Single(s => s.Name == "Šišanje").Quantity.Should().Be(2);
        d.TopProducts.Single().Should().Match<RankedItem>(p => p.Name == "Vosak" && p.Quantity == 3 && p.Revenue == 30m);
    }

    [Fact]
    public async Task Table_reports_carry_a_totals_row_that_matches_the_rows()
    {
        var (clientId, _) = await NewClient();
        var kerim = await KerimId();
        await Reservation(clientId, kerim, At(2037, 9, 2), 25, ReservationStatus.Completed);
        await Reservation(clientId, kerim, At(2037, 9, 3), 35, ReservationStatus.Completed);
        var admin = await Admin();

        foreach (var report in new[] { "revenue", "employees", "services" })
        {
            var t = (await admin.GetFromJsonAsync<TableDto>($"/api/v1/reports/{report}?from=2037-09-01&to=2037-09-30"))!;
            t.Columns.Should().NotBeEmpty();
            t.Rows.Should().OnlyContain(r => r.Count == t.Columns.Count, report);
            t.Totals.Should().NotBeNull().And.HaveCount(t.Columns.Count);
        }

        var revenue = (await admin.GetFromJsonAsync<TableDto>("/api/v1/reports/revenue?from=2037-09-01&to=2037-09-30"))!;
        var moneyCol = revenue.Columns.ToList().FindIndex(c => c.Key == "total");
        revenue.Rows.Sum(r => ((System.Text.Json.JsonElement)r[moneyCol]!).GetDecimal()).Should().Be(60m);
        ((System.Text.Json.JsonElement)revenue.Totals![moneyCol]!).GetDecimal().Should().Be(60m);
    }

    // ------------------------------------------------------------------ custom builder

    [Fact]
    public async Task The_builder_returns_only_the_chosen_fields_and_filters_by_status_and_employee()
    {
        var (clientId, _) = await NewClient();
        var kerim = await KerimId();
        var tarik = await TarikId();
        await Reservation(clientId, kerim, At(2037, 10, 2), 25, ReservationStatus.Completed);
        await Reservation(clientId, kerim, At(2037, 10, 3), 99, ReservationStatus.Cancelled);
        await Reservation(clientId, tarik, At(2037, 10, 4), 15, ReservationStatus.Completed);
        var admin = await Admin();
        var from = new DateOnly(2037, 10, 1);
        var to = new DateOnly(2037, 10, 31);

        var res = await admin.PostAsJsonAsync("/api/v1/reports/custom", new CustomReportRequest("Reservations", ["date", "employee", "price"], from, to, "Completed", kerim));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var t = (await res.Content.ReadFromJsonAsync<TableDto>())!;

        t.Columns.Select(c => c.Key).Should().Equal("date", "employee", "price");
        t.Rows.Should().ContainSingle();
        ((System.Text.Json.JsonElement)t.Rows[0][0]!).GetString().Should().Be("2037-10-02");
        ((System.Text.Json.JsonElement)t.Rows[0][1]!).GetString().Should().Be("Kerim Begić");
        ((System.Text.Json.JsonElement)t.Totals![2]!).GetDecimal().Should().Be(25m);
    }

    [Fact]
    public async Task The_builder_rejects_anything_that_is_not_on_its_menu()
    {
        var admin = await Admin();
        var from = new DateOnly(2037, 11, 1);
        foreach (var bad in new[]
                 {
                     new CustomReportRequest("AspNetUsers", ["date"], from, from),
                     new CustomReportRequest("Reservations", ["passwordHash"], from, from),
                     new CustomReportRequest("Reservations", ["date; DROP TABLE Reservations"], from, from),
                     new CustomReportRequest("Reservations", ["date"], from, from, "Completed'; --"),
                     new CustomReportRequest("Reservations", [], from, from),
                 })
            (await admin.PostAsJsonAsync("/api/v1/reports/custom", bad)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // and the data is still there
        (await admin.GetFromJsonAsync<TableDto>("/api/v1/reports/reservations?from=2037-01-01&to=2037-01-31")).Should().NotBeNull();
    }

    [Fact]
    public async Task The_builder_lists_its_menu()
    {
        var sources = (await (await Admin()).GetFromJsonAsync<List<CustomSource>>("/api/v1/reports/custom/fields"))!;
        sources.Select(s => s.Source).Should().BeEquivalentTo("Reservations", "Orders");
        sources.Single(s => s.Source == "Orders").Columns.Select(c => c.Key).Should().Contain("total");
    }

    [Fact]
    public async Task Order_reports_list_items_and_payment()
    {
        var (clientId, _) = await NewClient();
        await Order(clientId, At(2037, 12, 5), 24, OrderStatus.Fulfilled, "Dandy Pomade", qty: 2);
        var t = (await (await Admin()).GetFromJsonAsync<TableDto>("/api/v1/reports/orders?from=2037-12-01&to=2037-12-31"))!;

        t.Rows.Should().ContainSingle();
        var items = t.Columns.ToList().FindIndex(c => c.Key == "items");
        ((System.Text.Json.JsonElement)t.Rows[0][items]!).GetString().Should().Be("2× Dandy Pomade");
    }

    // ------------------------------------------------------------------ export

    [Fact]
    public async Task Csv_export_is_excel_friendly_and_defuses_formula_injection()
    {
        var (clientId, _) = await NewClient();
        var kerim = await KerimId();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.Users.Where(u => u.Id == clientId).ExecuteUpdateAsync(s => s.SetProperty(u => u.FirstName, "=HYPERLINK(\"http://evil\")"));
        }
        await Reservation(clientId, kerim, At(2037, 1, 6), 25, ReservationStatus.Completed);

        var res = await (await Admin()).GetAsync("/api/v1/reports/reservations/export?from=2037-01-01&to=2037-01-31&format=csv");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        res.Content.Headers.ContentDisposition!.FileName.Should().EndWith(".csv");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
        var text = Encoding.UTF8.GetString(bytes);
        text.Should().Contain("Datum;Vrijeme;Klijent");
        text.Should().Contain("\"'=HYPERLINK(\"\"http://evil\"\")", "a leading = is neutralised and quotes are doubled");
        text.Should().NotContain(";=HYPERLINK").And.NotContain("\n=HYPERLINK");
    }

    [Fact]
    public async Task Xlsx_export_is_a_real_workbook_and_formula_text_stays_text()
    {
        var (clientId, _) = await NewClient();
        var kerim = await KerimId();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.Users.Where(u => u.Id == clientId).ExecuteUpdateAsync(s => s.SetProperty(u => u.FirstName, "=1+1"));
        }
        await Reservation(clientId, kerim, At(2037, 2, 6), 25, ReservationStatus.Completed);

        var res = await (await Admin()).GetAsync("/api/v1/reports/reservations/export?from=2037-02-01&to=2037-02-28&format=xlsx");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        await using var stream = await res.Content.ReadAsStreamAsync();
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);
        ws.Cell(1, 1).GetString().Should().Be("Datum");
        var clientCell = ws.Cell(2, 3);
        clientCell.DataType.Should().Be(XLDataType.Text);
        clientCell.HasFormula.Should().BeFalse();
        clientCell.GetString().Should().StartWith("=1+1");
    }

    [Fact]
    public async Task Pdf_export_is_a_pdf_with_the_right_name()
    {
        var res = await (await Admin()).GetAsync("/api/v1/reports/revenue/export?from=2037-03-01&to=2037-03-31&format=pdf");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        res.Content.Headers.ContentDisposition!.FileName.Should().Be("prihod-01-03-2037-31-03-2037.pdf");
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes.Take(5).ToArray()).Should().Be("%PDF-");
        bytes.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task A_custom_report_can_be_exported_too_and_bad_requests_still_fail_cleanly()
    {
        var admin = await Admin();
        var from = new DateOnly(2037, 5, 1);
        var ok = await admin.PostAsJsonAsync("/api/v1/reports/custom/export?format=csv", new CustomReportRequest("Orders", ["date", "total"], from, from.AddDays(30)));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        var bad = await admin.PostAsJsonAsync("/api/v1/reports/custom/export?format=pdf", new CustomReportRequest("Orders", ["nope"], from, from));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
