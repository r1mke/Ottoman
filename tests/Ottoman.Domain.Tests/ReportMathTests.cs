using FluentAssertions;
using Ottoman.Domain.Reports;

namespace Ottoman.Domain.Tests;

public class ReportMathTests
{
    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void Days_are_counted_inclusively() => ReportMath.DaysIn(D(2030, 1, 1), D(2030, 1, 31)).Should().Be(31);

    [Theory]
    [InlineData(2030, 1, 10, 2030, 1, 1, false)]   // ends before it starts
    [InlineData(2030, 1, 1, 2030, 1, 1, true)]
    [InlineData(2030, 1, 1, 2032, 6, 1, false)]    // far too long
    public void Range_validation(int y1, int m1, int d1, int y2, int m2, int d2, bool ok) =>
        (ReportMath.ValidateRange(D(y1, m1, d1), D(y2, m2, d2), D(2030, 6, 1)) is null).Should().Be(ok);

    [Fact]
    public void Missing_ancient_and_far_future_dates_are_refused_so_date_maths_cannot_overflow()
    {
        var today = D(2026, 11, 8);
        ReportMath.ValidateRange(default, default, today).Should().Be("Odaberite početni i završni datum.");
        ReportMath.ValidateRange(default, D(2026, 1, 1), today).Should().NotBeNull();
        ReportMath.ValidateRange(D(1999, 12, 31), D(2000, 1, 5), today).Should().Contain("2000");
        ReportMath.ValidateRange(D(9999, 12, 1), D(9999, 12, 31), today).Should().Contain("godinu");
        ReportMath.ValidateRange(D(2027, 11, 1), D(2027, 11, 9), today).Should().Contain("godinu");
        ReportMath.ValidateRange(D(2000, 1, 1), D(2000, 1, 1), today).Should().BeNull();
        ReportMath.ValidateRange(D(2027, 11, 1), D(2027, 11, 8), today).Should().BeNull();
    }

    [Fact]
    public void The_previous_period_is_the_same_length_right_before()
    {
        var (from, to) = ReportMath.PreviousPeriod(D(2030, 3, 11), D(2030, 3, 20)); // 10 days
        from.Should().Be(D(2030, 3, 1));
        to.Should().Be(D(2030, 3, 10));
    }

    [Fact]
    public void The_previous_period_of_a_single_day_is_yesterday()
    {
        var (from, to) = ReportMath.PreviousPeriod(D(2030, 3, 11), D(2030, 3, 11));
        (from, to).Should().Be((D(2030, 3, 10), D(2030, 3, 10)));
    }

    [Theory]
    [InlineData(1, Granularity.Day)]
    [InlineData(31, Granularity.Day)]
    [InlineData(32, Granularity.Week)]
    [InlineData(190, Granularity.Week)]
    [InlineData(191, Granularity.Month)]
    [InlineData(700, Granularity.Month)]
    public void Granularity_follows_the_length(int days, Granularity expected) =>
        ReportMath.AutoGranularity(D(2030, 1, 1), D(2030, 1, 1).AddDays(days - 1)).Should().Be(expected);

    [Fact]
    public void Weeks_start_on_monday()
    {
        // 2030-01-06 is a Sunday, 2030-01-07 a Monday
        ReportMath.BucketStart(D(2030, 1, 6), Granularity.Week).Should().Be(D(2029, 12, 31));
        ReportMath.BucketStart(D(2030, 1, 7), Granularity.Week).Should().Be(D(2030, 1, 7));
        ReportMath.BucketStart(D(2030, 1, 9), Granularity.Week).Should().Be(D(2030, 1, 7));
    }

    [Fact]
    public void Months_start_on_the_first() =>
        ReportMath.BucketStart(D(2030, 2, 17), Granularity.Month).Should().Be(D(2030, 2, 1));

    [Fact]
    public void Buckets_include_empty_days_so_charts_have_no_gaps() =>
        ReportMath.Buckets(D(2030, 1, 1), D(2030, 1, 5), Granularity.Day).Should().HaveCount(5);

    [Fact]
    public void Week_buckets_cover_a_range_that_starts_mid_week()
    {
        // Wed 2030-01-02 .. Tue 2030-01-15 touches weeks starting 12-31, 01-07, 01-14
        ReportMath.Buckets(D(2030, 1, 2), D(2030, 1, 15), Granularity.Week)
            .Should().Equal(D(2029, 12, 31), D(2030, 1, 7), D(2030, 1, 14));
    }

    [Fact]
    public void Month_buckets_span_a_year_boundary() =>
        ReportMath.Buckets(D(2029, 11, 20), D(2030, 2, 3), Granularity.Month)
            .Should().Equal(D(2029, 11, 1), D(2029, 12, 1), D(2030, 1, 1), D(2030, 2, 1));

    [Fact]
    public void Every_date_falls_into_one_of_the_buckets()
    {
        var from = D(2030, 1, 2);
        var to = D(2030, 3, 29);
        foreach (var g in new[] { Granularity.Day, Granularity.Week, Granularity.Month })
        {
            var buckets = ReportMath.Buckets(from, to, g).ToHashSet();
            for (var d = from; d <= to; d = d.AddDays(1))
                buckets.Should().Contain(ReportMath.BucketStart(d, g), $"{d} in {g}");
        }
    }

    [Theory]
    [InlineData(150, 100, 50.0)]
    [InlineData(50, 100, -50.0)]
    [InlineData(100, 100, 0.0)]
    [InlineData(0, 0, 0.0)]
    public void Percent_change(decimal current, decimal previous, double expected) =>
        ReportMath.PercentChange(current, previous).Should().Be(expected);

    [Fact]
    public void Growth_from_nothing_has_no_percentage() => ReportMath.PercentChange(10, 0).Should().BeNull();

    [Fact]
    public void Percent_change_is_rounded_to_one_decimal() =>
        ReportMath.PercentChange(1, 3).Should().Be(-66.7);

    [Fact]
    public void Rates_and_averages_survive_an_empty_denominator()
    {
        ReportMath.Rate(1, 0).Should().Be(0);
        ReportMath.Rate(1, 8).Should().Be(12.5);
        ReportMath.Average(10, 0).Should().Be(0);
        ReportMath.Average(10, 3).Should().Be(3.33m);
    }

    [Theory]
    [InlineData(Granularity.Day, "05.01.")]
    [InlineData(Granularity.Week, "05.01.")]
    [InlineData(Granularity.Month, "01/2030")]
    public void Labels(Granularity g, string expected) =>
        ReportMath.Label(g == Granularity.Month ? D(2030, 1, 1) : D(2030, 1, 5), g).Should().Be(expected);
}
