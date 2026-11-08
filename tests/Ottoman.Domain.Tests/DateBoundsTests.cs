using FluentAssertions;
using Ottoman.Domain.Reservations;
using Ottoman.Domain.Scheduling;

namespace Ottoman.Domain.Tests;

public class DateBoundsTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);

    [Fact]
    public void The_availability_window_runs_from_yesterday_to_the_booking_limit_plus_a_day()
    {
        BookingPolicy.DateWindow(Today).Should().Be((new DateOnly(2026, 11, 7), Today.AddDays(BookingPolicy.MaxAdvanceDays + 1)));
        BookingPolicy.InDateWindow(Today, Today).Should().BeTrue();
        BookingPolicy.InDateWindow(Today.AddDays(-1), Today).Should().BeTrue();
        BookingPolicy.InDateWindow(Today.AddDays(-2), Today).Should().BeFalse();
        BookingPolicy.InDateWindow(Today.AddDays(BookingPolicy.MaxAdvanceDays + 1), Today).Should().BeTrue();
        BookingPolicy.InDateWindow(Today.AddDays(BookingPolicy.MaxAdvanceDays + 2), Today).Should().BeFalse();
        BookingPolicy.InDateWindow(DateOnly.MinValue, Today).Should().BeFalse();
        BookingPolicy.InDateWindow(DateOnly.MaxValue, Today).Should().BeFalse();
    }

    [Fact]
    public void A_month_counts_when_it_shares_a_day_with_the_window()
    {
        BookingPolicy.MonthInDateWindow(new DateOnly(2026, 11, 1), Today).Should().BeTrue();
        BookingPolicy.MonthInDateWindow(new DateOnly(2027, 2, 1), Today).Should().BeTrue(); // the window ends 2027-02-07
        BookingPolicy.MonthInDateWindow(new DateOnly(2027, 3, 1), Today).Should().BeFalse();
        BookingPolicy.MonthInDateWindow(new DateOnly(2026, 10, 1), Today).Should().BeFalse();
        BookingPolicy.MonthInDateWindow(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)).Should().BeTrue("yesterday was in October");
        BookingPolicy.MonthInDateWindow(DateOnly.MinValue, Today).Should().BeFalse();
        BookingPolicy.MonthInDateWindow(DateOnly.MaxValue, Today).Should().BeFalse();
    }

    [Fact]
    public void Filter_dates_are_pulled_into_a_range_that_converts_safely()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus14", TimeSpan.FromHours(14), "plus14", "plus14");
        var low = LocalTime.ClampFilterDate(DateOnly.MinValue);
        var high = LocalTime.ClampFilterDate(DateOnly.MaxValue);
        low.Should().Be(new DateOnly(1900, 1, 1));
        high.Should().Be(new DateOnly(9000, 12, 31));
        FluentActions.Invoking(() => LocalTime.DayRangeUtc(low, zone)).Should().NotThrow();
        FluentActions.Invoking(() => LocalTime.DayRangeUtc(high, zone)).Should().NotThrow();
        LocalTime.ClampFilterDate(Today).Should().Be(Today);
    }
}
