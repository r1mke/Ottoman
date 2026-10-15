using FluentAssertions;
using Ottoman.Domain.Reservations;

namespace Ottoman.Domain.Tests;

public class ReservationStateMachineTests
{
    private static readonly DateTime Now = new(2030, 1, 7, 8, 0, 0, DateTimeKind.Utc);
    private static DateTime In(int minutes) => Now.AddMinutes(minutes);

    private static Transition Check(ReservationStatus from, ReservationStatus to, ActorKind actor, DateTime start, int window = 120) =>
        ReservationStateMachine.Can(from, to, actor, Now, start, window);

    // ------------------------------------------------------------- cancelling

    [Fact]
    public void Client_can_cancel_before_the_cancellation_window()
    {
        Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Client, In(180)).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Client_can_cancel_exactly_at_the_window_boundary()
    {
        Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Client, In(120)).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Client_cannot_cancel_inside_the_window_and_is_told_why()
    {
        var t = Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Client, In(119));

        t.Allowed.Should().BeFalse();
        t.Reason.Should().Contain("2 h");
    }

    [Fact]
    public void Client_cannot_cancel_once_the_appointment_started()
    {
        Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Client, In(-5)).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Employee_can_cancel_up_to_the_start_regardless_of_the_window()
    {
        Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Employee, In(10)).Allowed.Should().BeTrue();
        Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Employee, In(-1)).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Admin_can_always_cancel_an_open_reservation()
    {
        Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Admin, In(5)).Allowed.Should().BeTrue();
        Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Admin, In(-30)).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Zero_window_lets_clients_cancel_until_the_start()
    {
        Check(ReservationStatus.Confirmed, ReservationStatus.Cancelled, ActorKind.Client, In(1), window: 0).Allowed.Should().BeTrue();
    }

    // -------------------------------------------------------------- finishing

    [Theory]
    [InlineData(ReservationStatus.Completed)]
    [InlineData(ReservationStatus.NoShow)]
    public void Staff_can_finish_only_after_the_start(ReservationStatus outcome)
    {
        Check(ReservationStatus.Confirmed, outcome, ActorKind.Employee, In(-1)).Allowed.Should().BeTrue();
        Check(ReservationStatus.Confirmed, outcome, ActorKind.Admin, In(0)).Allowed.Should().BeTrue();
        Check(ReservationStatus.Confirmed, outcome, ActorKind.Employee, In(1)).Allowed.Should().BeFalse();
    }

    [Theory]
    [InlineData(ReservationStatus.Completed)]
    [InlineData(ReservationStatus.NoShow)]
    public void Clients_can_never_finish_an_appointment(ReservationStatus outcome)
    {
        Check(ReservationStatus.Confirmed, outcome, ActorKind.Client, In(-60)).Allowed.Should().BeFalse();
    }

    // -------------------------------------------------------------- terminal

    [Theory]
    [InlineData(ReservationStatus.Completed)]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.NoShow)]
    public void Closed_reservations_cannot_change_for_anyone(ReservationStatus closed)
    {
        foreach (var actor in Enum.GetValues<ActorKind>())
            foreach (var target in new[] { ReservationStatus.Cancelled, ReservationStatus.Completed, ReservationStatus.NoShow, ReservationStatus.Confirmed })
                Check(closed, target, actor, In(-60)).Allowed.Should().BeFalse($"{closed} -> {target} by {actor}");
    }

    [Fact]
    public void Only_staff_can_confirm_a_pending_reservation()
    {
        Check(ReservationStatus.Pending, ReservationStatus.Confirmed, ActorKind.Client, In(60)).Allowed.Should().BeFalse();
        Check(ReservationStatus.Pending, ReservationStatus.Confirmed, ActorKind.Employee, In(60)).Allowed.Should().BeTrue();
    }

    [Fact]
    public void Pending_cannot_be_completed_directly()
    {
        Check(ReservationStatus.Pending, ReservationStatus.Completed, ActorKind.Admin, In(-10)).Allowed.Should().BeFalse();
    }
}

public class BookingPolicyTests
{
    private static readonly DateTime Now = new(2030, 1, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_normal_booking_has_no_errors() =>
        BookingPolicy.Validate(Now.AddDays(2), Now, 2, 0, "Fade", bypassClientLimit: false).Should().BeEmpty();

    [Fact]
    public void Booking_too_far_ahead_is_rejected() =>
        BookingPolicy.Validate(Now.AddDays(91), Now, 1, 0, null, false).Should().ContainSingle(e => e.Contains("90"));

    [Fact]
    public void Booking_exactly_at_the_horizon_is_allowed() =>
        BookingPolicy.Validate(Now.AddDays(90), Now, 1, 0, null, false).Should().BeEmpty();

    [Fact]
    public void Too_many_active_reservations_block_clients_but_not_staff()
    {
        BookingPolicy.Validate(Now.AddDays(1), Now, 1, 5, null, bypassClientLimit: false).Should().NotBeEmpty();
        BookingPolicy.Validate(Now.AddDays(1), Now, 1, 5, null, bypassClientLimit: true).Should().BeEmpty();
        BookingPolicy.Validate(Now.AddDays(1), Now, 1, 4, null, bypassClientLimit: false).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Service_count_must_be_between_one_and_ten(int count) =>
        BookingPolicy.Validate(Now.AddDays(1), Now, count, 0, null, false).Should().NotBeEmpty();

    [Fact]
    public void Overlong_note_is_rejected() =>
        BookingPolicy.Validate(Now.AddDays(1), Now, 1, 0, new string('x', 501), false).Should().NotBeEmpty();
}

public class SlotGridTests
{
    private static readonly DateTime Start = new(2030, 1, 7, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_thirty_minute_booking_occupies_six_five_minute_steps()
    {
        var slots = SlotGrid.Occupied(Start, 30, 5);

        slots.Should().HaveCount(6);
        slots.First().Should().Be(Start);
        slots.Last().Should().Be(Start.AddMinutes(25)); // the end instant itself is NOT occupied
    }

    [Fact]
    public void Back_to_back_bookings_do_not_share_a_step()
    {
        var first = SlotGrid.Occupied(Start, 30, 5);
        var second = SlotGrid.Occupied(Start.AddMinutes(30), 30, 5);

        first.Intersect(second).Should().BeEmpty();
    }

    [Fact]
    public void Overlapping_bookings_share_at_least_one_step()
    {
        var a = SlotGrid.Occupied(Start, 30, 5);
        var b = SlotGrid.Occupied(Start.AddMinutes(25), 30, 5);

        a.Intersect(b).Should().ContainSingle().Which.Should().Be(Start.AddMinutes(25));
    }

    [Fact]
    public void Duration_that_is_not_a_multiple_of_the_step_is_rejected()
    {
        var act = () => SlotGrid.Occupied(Start, 32, 5);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(30, 0)]
    public void Non_positive_inputs_are_rejected(int duration, int step)
    {
        var act = () => SlotGrid.Occupied(Start, duration, step);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
