using FluentAssertions;
using Ottoman.Application.Employees;
using Ottoman.Application.Reports;
using Ottoman.Application.Reviews;
using Ottoman.Application.Shop;

namespace Ottoman.Application.Tests;

public class RequestValidatorTests
{
    [Fact]
    public void Hiding_a_review_needs_an_explicit_choice()
    {
        var v = new HideRequestValidator();
        v.Validate(new HideRequest(true)).IsValid.Should().BeTrue();
        v.Validate(new HideRequest(false)).IsValid.Should().BeTrue();
        v.Validate(new HideRequest(null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Employee_services_are_positive_distinct_ids_and_may_be_empty()
    {
        var v = new SetEmployeeServicesRequestValidator();
        v.Validate(new SetEmployeeServicesRequest([])).IsValid.Should().BeTrue();
        v.Validate(new SetEmployeeServicesRequest([1, 2, 3])).IsValid.Should().BeTrue();
        v.Validate(new SetEmployeeServicesRequest([1, 1])).IsValid.Should().BeFalse();
        v.Validate(new SetEmployeeServicesRequest([0])).IsValid.Should().BeFalse();
        v.Validate(new SetEmployeeServicesRequest(null!)).IsValid.Should().BeFalse();
        v.Validate(new SetEmployeeServicesRequest(Enumerable.Range(1, 101).ToList())).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_section_order_lists_each_section_once()
    {
        var v = new ShopSectionOrderRequestValidator();
        v.Validate(new ShopSectionOrderRequest([3, 1, 2])).IsValid.Should().BeTrue();
        v.Validate(new ShopSectionOrderRequest([])).IsValid.Should().BeFalse();
        v.Validate(new ShopSectionOrderRequest([1, 1])).IsValid.Should().BeFalse();
        v.Validate(new ShopSectionOrderRequest([-1])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_picked_card_picture_must_name_a_picture()
    {
        var v = new ShopSectionPickedImageRequestValidator();
        v.Validate(new ShopSectionPickedImageRequest(7)).IsValid.Should().BeTrue();
        v.Validate(new ShopSectionPickedImageRequest(0)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_custom_report_is_checked_against_the_menu_at_the_edge()
    {
        var v = new CustomReportRequestValidator();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);
        v.Validate(new CustomReportRequest(CustomReportSources.Reservations, ["date", "price"], from, to)).IsValid.Should().BeTrue();
        v.Validate(new CustomReportRequest("Users", ["date"], from, to)).IsValid.Should().BeFalse();
        v.Validate(new CustomReportRequest(CustomReportSources.Reservations, ["password"], from, to)).IsValid.Should().BeFalse();
        var result = v.Validate(new CustomReportRequest(CustomReportSources.Orders, [], from, to));
        result.IsValid.Should().BeFalse();
        result.Errors.Single().ErrorMessage.Should().Contain("barem jedno polje");
    }
}
