using FluentAssertions;
using Ottoman.Domain.Reservations;

namespace Ottoman.Domain.Tests;

public class ReservationConfirmationTests
{
    private static readonly DateTime Now = new(2030, 1, 7, 8, 0, 0, DateTimeKind.Utc);

    private static Transition Confirm(ReservationStatus from, ActorKind actor) =>
        ReservationStateMachine.Can(from, ReservationStatus.Confirmed, actor, Now, Now.AddHours(5), 120);

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Every_booking_starts_pending_and_the_system_confirms_it_only_when_the_salon_auto_confirms(bool auto, bool systemConfirms)
    {
        var (initial, confirms) = ReservationStateMachine.OnCreate(auto);
        initial.Should().Be(ReservationStatus.Pending);
        confirms.Should().Be(systemConfirms);
    }

    [Theory]
    [InlineData(ActorKind.System)]
    [InlineData(ActorKind.Employee)]
    [InlineData(ActorKind.Admin)]
    public void The_salon_and_the_system_confirm_a_pending_booking(ActorKind actor) =>
        Confirm(ReservationStatus.Pending, actor).Allowed.Should().BeTrue();

    [Fact]
    public void A_client_never_confirms_and_nothing_is_confirmed_twice()
    {
        Confirm(ReservationStatus.Pending, ActorKind.Client).Allowed.Should().BeFalse();
        Confirm(ReservationStatus.Confirmed, ActorKind.Admin).Allowed.Should().BeFalse();
        Confirm(ReservationStatus.Cancelled, ActorKind.Admin).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Only_staff_decline_and_only_a_pending_booking()
    {
        ReservationStateMachine.CanDecline(ReservationStatus.Pending, ActorKind.Employee).Allowed.Should().BeTrue();
        ReservationStateMachine.CanDecline(ReservationStatus.Pending, ActorKind.Admin).Allowed.Should().BeTrue();
        ReservationStateMachine.CanDecline(ReservationStatus.Pending, ActorKind.Client).Allowed.Should().BeFalse();
        ReservationStateMachine.CanDecline(ReservationStatus.Pending, ActorKind.System).Allowed.Should().BeFalse();
        ReservationStateMachine.CanDecline(ReservationStatus.Confirmed, ActorKind.Admin).Allowed.Should().BeFalse();
    }

    [Fact]
    public void A_pending_booking_cannot_be_completed_before_it_is_confirmed()
    {
        ReservationStateMachine.Can(ReservationStatus.Pending, ReservationStatus.Completed, ActorKind.Employee, Now, Now.AddHours(-1), 120)
            .Allowed.Should().BeFalse();
    }

    [Fact]
    public void Declining_needs_a_reason()
    {
        CancellationRules.ReasonProblem(ActorKind.Employee, null).Should().NotBeNull();
        CancellationRules.ReasonProblem(ActorKind.Employee, "Bolestan").Should().BeNull();
    }
}
