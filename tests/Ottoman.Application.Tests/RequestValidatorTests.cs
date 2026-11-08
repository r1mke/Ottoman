using FluentAssertions;
using Ottoman.Application.Auth;
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
    public void A_photo_zoom_is_required_and_from_one_to_two()
    {
        var v = new UpdatePhotoZoomRequestValidator();
        v.Validate(new UpdatePhotoZoomRequest(1.0)).IsValid.Should().BeTrue();
        v.Validate(new UpdatePhotoZoomRequest(1.6)).IsValid.Should().BeTrue();
        v.Validate(new UpdatePhotoZoomRequest(2.0)).IsValid.Should().BeTrue();
        v.Validate(new UpdatePhotoZoomRequest(null)).IsValid.Should().BeFalse();
        v.Validate(new UpdatePhotoZoomRequest(0.5)).IsValid.Should().BeFalse();
        v.Validate(new UpdatePhotoZoomRequest(2.5)).IsValid.Should().BeFalse();
        v.Validate(new UpdatePhotoZoomRequest(double.NaN)).IsValid.Should().BeFalse();
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
        var v = new CustomReportRequestValidator(new FixedClock(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));
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

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public class TimeOffClashesTests
{
    private static Ottoman.Application.Employees.TimeOffClashDto Clash(int i) =>
        new(i, $"Klijent {i}", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(30), $"0{i}.12.2026. 10:00");

    [Fact]
    public void The_message_names_who_and_when_and_how_many()
    {
        var text = Ottoman.Application.Employees.TimeOffClashes.Message([Clash(1), Clash(2)]);
        text.Should().StartWith("Odsustvo se preklapa s aktivnim rezervacijama (2): Klijent 1, 01.12.2026. 10:00; Klijent 2, 02.12.2026. 10:00.");
        text.Should().Contain("potvrdite");
    }

    [Fact]
    public void A_long_list_is_cut_after_five()
    {
        var text = Ottoman.Application.Employees.TimeOffClashes.Message(Enumerable.Range(1, 8).Select(Clash).ToList());
        text.Should().Contain("(8)").And.Contain("Klijent 5").And.NotContain("Klijent 6").And.Contain("i još 3");
    }
}

public class EmployeeTitleChoiceTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData(0, "  ", false)]
    [InlineData(-1, null, false)]
    [InlineData(3, null, true)]
    [InlineData(null, "Barber", true)]
    public void A_title_is_given_by_id_or_by_name(int? id, string? name, bool given) =>
        Ottoman.Application.Employees.EmployeeTitleChoice.Given(id, name).Should().Be(given);

    [Fact]
    public void The_create_validator_asks_for_a_title()
    {
        var v = new Ottoman.Application.Employees.CreateEmployeeRequestValidator();
        var r = new Ottoman.Application.Employees.CreateEmployeeRequest("Ana", "Anić", "ana@x.ba", null, "Jaka_Lozinka1!", null, null, null);
        v.Validate(r).Errors.Select(e => e.ErrorMessage).Should().Contain("Odaberite titulu.");
        v.Validate(r with { TitleId = 2 }).Errors.Select(e => e.ErrorMessage).Should().NotContain("Odaberite titulu.");
    }
}
