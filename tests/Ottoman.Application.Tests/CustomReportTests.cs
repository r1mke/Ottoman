using FluentAssertions;
using Ottoman.Application.Reports;

namespace Ottoman.Application.Tests;

public class CustomReportTests
{
    private static readonly DateOnly From = new(2030, 1, 1);
    private static readonly DateOnly To = new(2030, 1, 31);

    private static CustomReportRequest Req(
        string source = "Reservations", string[]? columns = null, string? status = null, string? employee = null,
        DateOnly? from = null, DateOnly? to = null) =>
        new(source, columns ?? ["date", "client"], from ?? From, to ?? To, status, employee);

    [Fact]
    public void A_request_made_of_menu_choices_is_valid() =>
        CustomReportCatalog.Validate(Req(columns: ["date", "client", "price"], status: "Completed")).Should().BeNull();

    [Theory]
    [InlineData("Users")]
    [InlineData("reservations")] // case matters: the menu is exact
    [InlineData("")]
    [InlineData("Reservations; DROP TABLE Users")]
    public void Unknown_sources_are_rejected(string source) =>
        CustomReportCatalog.Validate(Req(source)).Should().NotBeNull();

    [Theory]
    [InlineData("passwordHash")]
    [InlineData("client; --")]
    [InlineData("total")] // belongs to Orders, not Reservations
    public void Unknown_columns_are_rejected(string column) =>
        CustomReportCatalog.Validate(Req(columns: ["date", column])).Should().Contain("Nepoznato polje");

    [Fact]
    public void Columns_are_required_and_unique()
    {
        CustomReportCatalog.Validate(Req(columns: [])).Should().NotBeNull();
        CustomReportCatalog.Validate(Req(columns: ["date", "date"])).Should().NotBeNull();
    }

    [Fact]
    public void Status_must_be_one_of_the_sources_statuses()
    {
        CustomReportCatalog.Validate(Req(status: "Completed")).Should().BeNull();
        CustomReportCatalog.Validate(Req(status: "Fulfilled")).Should().NotBeNull();
        CustomReportCatalog.Validate(Req("Orders", ["date"], status: "Fulfilled")).Should().BeNull();
        CustomReportCatalog.Validate(Req(status: "x' OR '1'='1")).Should().NotBeNull();
    }

    [Fact]
    public void The_employee_filter_exists_only_for_reservations()
    {
        CustomReportCatalog.Validate(Req(employee: "e1")).Should().BeNull();
        CustomReportCatalog.Validate(Req("Orders", ["date"], employee: "e1")).Should().NotBeNull();
    }

    [Fact]
    public void The_period_is_validated()
    {
        CustomReportCatalog.Validate(Req(from: To, to: From)).Should().NotBeNull();
        CustomReportCatalog.Validate(Req(from: From, to: From.AddDays(2000))).Should().NotBeNull();
    }

    [Fact]
    public void Every_source_offers_columns_with_unique_keys()
    {
        foreach (var s in CustomReportCatalog.Sources)
        {
            s.Columns.Should().NotBeEmpty();
            s.Columns.Select(c => c.Key).Should().OnlyHaveUniqueItems();
            s.Statuses.Should().NotBeEmpty();
        }
    }
}
