using FluentAssertions;
using Ottoman.Application.Reports;

namespace Ottoman.Application.Tests;

public class CustomReportTests
{
    private static readonly DateOnly From = new(2030, 1, 1);
    private static readonly DateOnly To = new(2030, 1, 31);
    private static readonly DateOnly Today = new(2030, 2, 1);

    private static string? Validate(CustomReportRequest r) => CustomReportCatalog.Validate(r, Today);

    private static CustomReportRequest Req(
        string source = "Reservations", string[]? columns = null, string? status = null, string? employee = null,
        DateOnly? from = null, DateOnly? to = null) =>
        new(source, columns ?? ["date", "client"], from ?? From, to ?? To, status, employee);

    [Fact]
    public void A_request_made_of_menu_choices_is_valid() =>
        Validate(Req(columns: ["date", "client", "price"], status: "Completed")).Should().BeNull();

    [Theory]
    [InlineData("Users")]
    [InlineData("reservations")] // case matters: the menu is exact
    [InlineData("")]
    [InlineData("Reservations; DROP TABLE Users")]
    public void Unknown_sources_are_rejected(string source) =>
        Validate(Req(source)).Should().NotBeNull();

    [Theory]
    [InlineData("passwordHash")]
    [InlineData("client; --")]
    [InlineData("total")] // belongs to Orders, not Reservations
    public void Unknown_columns_are_rejected(string column) =>
        Validate(Req(columns: ["date", column])).Should().Contain("Nepoznato polje");

    [Fact]
    public void Columns_are_required_and_unique()
    {
        Validate(Req(columns: [])).Should().NotBeNull();
        Validate(Req(columns: ["date", "date"])).Should().NotBeNull();
    }

    [Fact]
    public void Status_must_be_one_of_the_sources_statuses()
    {
        Validate(Req(status: "Completed")).Should().BeNull();
        Validate(Req(status: "Fulfilled")).Should().NotBeNull();
        Validate(Req("Orders", ["date"], status: "Fulfilled")).Should().BeNull();
        Validate(Req(status: "x' OR '1'='1")).Should().NotBeNull();
    }

    [Fact]
    public void The_employee_filter_exists_only_for_reservations()
    {
        Validate(Req(employee: "e1")).Should().BeNull();
        Validate(Req("Orders", ["date"], employee: "e1")).Should().NotBeNull();
    }

    [Fact]
    public void The_period_is_validated()
    {
        Validate(Req(from: To, to: From)).Should().NotBeNull();
        Validate(Req(from: From, to: From.AddDays(2000))).Should().NotBeNull();
        Validate(Req(from: DateOnly.MinValue, to: DateOnly.MinValue)).Should().Contain("Odaberite");
        Validate(Req(from: new DateOnly(1999, 12, 31))).Should().Contain("2000");
        Validate(Req(to: Today.AddYears(1).AddDays(1))).Should().Contain("godinu");
        Validate(Req(from: new DateOnly(9999, 12, 1), to: new DateOnly(9999, 12, 31))).Should().NotBeNull();
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
