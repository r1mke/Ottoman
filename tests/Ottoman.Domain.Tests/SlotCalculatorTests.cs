using FluentAssertions;
using Ottoman.Domain.Scheduling;

namespace Ottoman.Domain.Tests;

public class SlotCalculatorTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");
    private static readonly DateOnly Monday = new(2030, 1, 7); // winter: UTC+1

    private static DateTime Utc(DateOnly date, int h, int m = 0) =>
        LocalTime.ToUtc(date, new TimeOnly(h, m), Zone);

    private static DateTime At(int h, int m = 0) => Utc(Monday, h, m);

    private static TimeRange Busy(int h1, int m1, int h2, int m2) => new(At(h1, m1), At(h2, m2));

    private static SlotQuery Query(
        int duration = 30,
        IReadOnlyList<TimeRange>? busy = null,
        WorkingDay? working = null,
        DateTime? now = null,
        int lead = 0,
        int shortest = 15,
        DateOnly? date = null) =>
        new(
            date ?? Monday,
            Zone,
            working ?? new WorkingDay(new TimeOnly(9, 0), new TimeOnly(17, 0)),
            busy ?? [],
            duration,
            StepMinutes: 5,
            OfferGridMinutes: 15,
            MinLeadMinutes: lead,
            NowUtc: now ?? new DateTime(2029, 12, 31, 0, 0, 0, DateTimeKind.Utc), // a week before Monday
            ShortestServiceMinutes: shortest);

    // ---------------------------------------------------------------- basics

    [Fact]
    public void Nothing_is_offered_beyond_the_booking_limit()
    {
        // Now is Monday 12:00 local minus 90 days: the day is exactly at the limit, so only starts up to 12:00 count.
        var now = At(12).AddDays(-Reservations.BookingPolicy.MaxAdvanceDays);
        var all = SlotCalculator.AllStarts(Query(now: now));
        all.Should().NotBeEmpty();
        all.Max().Should().Be(At(12));
        SlotCalculator.OfferedStarts(Query(now: now)).Should().OnlyContain(s => s <= At(12));

        // A day after the limit: nothing at all.
        SlotCalculator.AllStarts(Query(now: now.AddDays(-1))).Should().BeEmpty();
        // Well inside the limit the whole day is open again.
        SlotCalculator.AllStarts(Query(now: now.AddDays(1))).Max().Should().Be(At(16, 30));
    }

    [Fact]
    public void No_working_day_means_no_slots()
    {
        var q = Query() with { Working = null };
        SlotCalculator.AllStarts(q).Should().BeEmpty();
        SlotCalculator.OfferedStarts(q).Should().BeEmpty();
    }

    [Fact]
    public void Free_day_has_a_start_every_five_minutes_until_the_service_no_longer_fits()
    {
        var starts = SlotCalculator.AllStarts(Query(duration: 30));

        starts.First().Should().Be(At(9));
        starts.Last().Should().Be(At(16, 30)); // 16:30 + 30 = 17:00 close
        starts.Should().HaveCount(91);         // (7.5 h / 5 min) + 1
    }

    [Fact]
    public void Service_longer_than_the_day_has_no_slots() =>
        SlotCalculator.AllStarts(Query(duration: 9 * 60)).Should().BeEmpty();

    // -------------------------------------------------- the user's examples

    [Fact]
    public void Existing_booking_at_0930_leaves_only_starts_that_end_by_0930()
    {
        var starts = SlotCalculator.AllStarts(Query(duration: 30, busy: [Busy(9, 30, 10, 0)]));

        starts.Where(s => s < At(10)).Should().Equal(At(9));            // 30 min: only 09:00 fits before 09:30
        starts.Should().Contain(At(10));                                 // next free start after the booking
        starts.Should().NotContain(At(9, 5));
    }

    [Fact]
    public void Shorter_service_before_the_booking_can_start_at_0900_or_0905()
    {
        var starts = SlotCalculator.AllStarts(Query(duration: 25, busy: [Busy(9, 30, 10, 0)]));

        starts.Where(s => s < At(10)).Should().Equal(At(9), At(9, 5)); // 09:05 + 25 = 09:30
    }

    [Fact]
    public void After_a_booking_ending_at_0940_the_next_offered_start_is_0940()
    {
        var q = Query(duration: 30, busy: [Busy(9, 10, 9, 40)]);

        var offered = SlotCalculator.OfferedStarts(q);

        offered.Should().Contain(At(9, 40));          // flush with the booking
        offered.Should().NotContain(At(9, 45));       // would leave a 5-minute hole
        offered.Should().Contain(At(10));             // grid start, hole is 20 min >= shortest service
    }

    // ------------------------------------------------------------- breaks etc.

    [Fact]
    public void Break_blocks_starts_that_would_overlap_it()
    {
        var working = new WorkingDay(new TimeOnly(9, 0), new TimeOnly(17, 0), new TimeOnly(12, 0), new TimeOnly(12, 30));
        var starts = SlotCalculator.AllStarts(Query(duration: 30, working: working));

        starts.Should().Contain(At(11, 30));
        starts.Should().NotContain(At(11, 35));
        starts.Should().NotContain(At(12, 0));
        starts.Should().NotContain(At(12, 25));
        starts.Should().Contain(At(12, 30));
    }

    [Fact]
    public void Time_off_covering_the_morning_removes_morning_slots()
    {
        var starts = SlotCalculator.AllStarts(Query(busy: [Busy(0, 0, 13, 0)]));

        starts.Should().OnlyContain(s => s >= At(13));
        starts.First().Should().Be(At(13));
    }

    [Fact]
    public void Busy_interval_outside_opening_hours_is_ignored()
    {
        var starts = SlotCalculator.AllStarts(Query(busy: [Busy(6, 0, 8, 0)]));
        starts.First().Should().Be(At(9));
    }

    [Fact]
    public void Overlapping_busy_intervals_are_merged()
    {
        var q = Query(duration: 15, busy: [Busy(10, 0, 11, 0), Busy(10, 30, 11, 30)]);

        var windows = SlotCalculator.FreeWindows(q);

        windows.Should().Equal(new TimeRange(At(9), At(10)), new TimeRange(At(11, 30), At(17)));
    }

    [Fact]
    public void Starts_are_aligned_to_the_step_after_an_irregular_busy_end()
    {
        // Time off ends at 09:22 -> the first valid start is 09:25, never 09:22.
        var q = Query(duration: 30, busy: [new TimeRange(At(8, 0), At(9, 22))]);

        SlotCalculator.AllStarts(q).First().Should().Be(At(9, 25));
    }

    // ---------------------------------------------------------- lead / past

    [Fact]
    public void Minimum_lead_time_hides_slots_that_are_too_soon()
    {
        var now = At(9, 50); // lead 30 -> earliest 10:20
        var starts = SlotCalculator.AllStarts(Query(now: now, lead: 30));

        starts.First().Should().Be(At(10, 20));
    }

    [Fact]
    public void A_day_entirely_in_the_past_has_no_slots()
    {
        var q = Query(now: At(18));
        SlotCalculator.AllStarts(q).Should().BeEmpty();
    }

    // -------------------------------------------------------------- offering

    [Fact]
    public void Free_day_offers_the_fifteen_minute_grid_only()
    {
        var offered = SlotCalculator.OfferedStarts(Query(duration: 30));

        offered.Should().Contain([At(9), At(9, 15), At(9, 30), At(16, 30)]);
        offered.Should().NotContain(At(9, 5));
        offered.Should().NotContain(At(9, 10));
    }

    [Fact]
    public void Show_all_includes_starts_the_default_offer_hides()
    {
        var q = Query(duration: 30);

        SlotCalculator.AllStarts(q).Should().Contain(At(9, 5));
        SlotCalculator.OfferedStarts(q).Should().NotContain(At(9, 5));
    }

    [Fact]
    public void Offered_starts_never_leave_a_gap_shorter_than_the_shortest_service()
    {
        // Free window 09:00-09:50, 30 min service, shortest service 15.
        var q = Query(duration: 30, busy: [Busy(0, 0, 9, 0), Busy(9, 50, 23, 0)]);

        var offered = SlotCalculator.OfferedStarts(q);

        offered.Should().Equal(At(9), At(9, 20)); // 09:15 would leave 5 min; 09:20 is flush with the end
    }

    // --------------------------------------------------------------- DST

    [Fact]
    public void Spring_forward_day_uses_the_correct_utc_offset_and_is_23_hours_long()
    {
        var date = new DateOnly(2030, 3, 31); // clocks go 02:00 -> 03:00
        var range = LocalTime.DayRangeUtc(date, Zone);

        range.Duration.Should().Be(TimeSpan.FromHours(23));

        var q = Query(duration: 30, date: date, now: new DateTime(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        SlotCalculator.AllStarts(q).First().Should().Be(new DateTime(2030, 3, 31, 7, 0, 0, DateTimeKind.Utc)); // 09:00 CEST = 07:00Z
    }

    [Fact]
    public void Fall_back_day_is_25_hours_long()
    {
        LocalTime.DayRangeUtc(new DateOnly(2030, 10, 27), Zone).Duration.Should().Be(TimeSpan.FromHours(25));
    }

    [Fact]
    public void A_nonexistent_local_time_moves_forward_past_the_gap()
    {
        var utc = LocalTime.ToUtc(new DateOnly(2030, 3, 31), new TimeOnly(2, 30), Zone);

        // 02:30 does not exist; the next valid wall time is 03:00 CEST = 01:00Z.
        utc.Should().Be(new DateTime(2030, 3, 31, 1, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void An_ambiguous_local_time_resolves_to_its_first_occurrence()
    {
        var utc = LocalTime.ToUtc(new DateOnly(2030, 10, 27), new TimeOnly(2, 30), Zone);

        utc.Should().Be(new DateTime(2030, 10, 27, 0, 30, 0, DateTimeKind.Utc)); // 02:30 CEST (+2)
    }

    // ------------------------------------------------------------ properties

    [Fact]
    public void Randomised_schedules_never_produce_overlapping_or_out_of_hours_starts()
    {
        var rng = new Random(12345);
        for (var iteration = 0; iteration < 300; iteration++)
        {
            var busy = new List<TimeRange>();
            for (var i = rng.Next(0, 6); i > 0; i--)
            {
                var startMin = rng.Next(8 * 60, 17 * 60);
                var len = rng.Next(1, 20) * 5;
                busy.Add(new TimeRange(At(0).AddMinutes(startMin), At(0).AddMinutes(startMin + len)));
            }
            var duration = rng.Next(1, 13) * 5;
            var q = Query(duration: duration, busy: busy);
            var open = new TimeRange(At(9), At(17));

            var all = SlotCalculator.AllStarts(q);
            var offered = SlotCalculator.OfferedStarts(q);

            offered.Should().BeSubsetOf(all);
            foreach (var start in all)
            {
                var range = new TimeRange(start, start.AddMinutes(duration));
                range.Start.Should().BeOnOrAfter(open.Start);
                range.End.Should().BeOnOrBefore(open.End);
                busy.Should().NotContain(b => b.Overlaps(range), $"{start:O} overlaps a busy interval (iteration {iteration})");
            }
            all.Should().BeInAscendingOrder();
        }
    }
}

public class WorkingDayTests
{
    private static TimeOnly T(int h, int m = 0) => new(h, m);

    [Fact]
    public void Valid_day_with_break_has_no_errors() =>
        new WorkingDay(T(9), T(17), T(12), T(12, 30)).Validate(5).Should().BeEmpty();

    [Fact]
    public void End_before_start_is_invalid() =>
        new WorkingDay(T(17), T(9)).Validate(5).Should().NotBeEmpty();

    [Fact]
    public void Break_outside_the_working_hours_is_invalid() =>
        new WorkingDay(T(9), T(17), T(8), T(9)).Validate(5).Should().NotBeEmpty();

    [Fact]
    public void Half_specified_break_is_invalid() =>
        new WorkingDay(T(9), T(17), T(12), null).Validate(5).Should().NotBeEmpty();

    [Fact]
    public void Times_off_the_step_grid_are_invalid() =>
        new WorkingDay(T(9, 3), T(17)).Validate(5).Should().NotBeEmpty();
}
