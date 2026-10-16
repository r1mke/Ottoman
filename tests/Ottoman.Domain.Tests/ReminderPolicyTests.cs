using FluentAssertions;
using Ottoman.Domain;
using Ottoman.Domain.Notifications;

namespace Ottoman.Domain.Tests;

public class ReminderPolicyTests
{
    private static readonly DateTime Start = new(2030, 6, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BookedLongAgo = Start.AddDays(-10);
    private static readonly HashSet<ReminderKind> None = [];

    private static IReadOnlyList<ReminderKind> Due(TimeSpan untilStart, DateTime? created = null, params ReminderKind[] sent) =>
        ReminderPolicy.Due(Start - untilStart, Start, created ?? BookedLongAgo, sent.ToHashSet());

    [Fact]
    public void Nothing_is_due_more_than_a_day_ahead() => Due(TimeSpan.FromHours(24.01)).Should().BeEmpty();

    [Theory]
    [InlineData(24)]
    [InlineData(12)]
    [InlineData(2.01)]
    public void The_day_reminder_is_due_between_24_and_2_hours_before(double hours) =>
        Due(TimeSpan.FromHours(hours)).Should().Equal(ReminderKind.Day);

    [Theory]
    [InlineData(2)]
    [InlineData(1)]
    [InlineData(0.01)]
    public void The_hours_reminder_is_due_in_the_last_two_hours(double hours) =>
        Due(TimeSpan.FromHours(hours)).Should().Equal(ReminderKind.Hours);

    [Fact]
    public void A_reminder_is_never_sent_twice() =>
        Due(TimeSpan.FromHours(10), null, ReminderKind.Day).Should().BeEmpty();

    [Fact]
    public void Missing_the_day_window_does_not_skip_the_hours_reminder() =>
        Due(TimeSpan.FromHours(1), null, ReminderKind.Day).Should().Equal(ReminderKind.Hours);

    [Fact]
    public void Nothing_is_due_once_the_appointment_has_started()
    {
        ReminderPolicy.Due(Start, Start, BookedLongAgo, None).Should().BeEmpty();
        ReminderPolicy.Due(Start.AddMinutes(5), Start, BookedLongAgo, None).Should().BeEmpty();
    }

    [Fact]
    public void A_booking_made_this_morning_gets_no_day_before_reminder()
    {
        // booked 5 hours before the visit: "see you tomorrow" would be wrong
        var created = Start.AddHours(-5);
        Due(TimeSpan.FromHours(4), created).Should().BeEmpty();
        Due(TimeSpan.FromHours(1.5), created).Should().Equal(ReminderKind.Hours);
    }

    [Fact]
    public void A_booking_made_minutes_before_the_visit_gets_no_reminder_at_all()
    {
        var created = Start.AddMinutes(-30);
        Due(TimeSpan.FromMinutes(20), created).Should().BeEmpty();
    }
}

public class AudienceRulesTests
{
    [Theory]
    [InlineData(AnnouncementAudience.Clients, new[] { "Client" })]
    [InlineData(AnnouncementAudience.Employees, new[] { "Employee" })]
    [InlineData(AnnouncementAudience.All, new[] { "Client", "Employee", "Admin" })]
    [InlineData(AnnouncementAudience.User, new string[0])]
    public void Roles_per_audience(AnnouncementAudience audience, string[] roles) =>
        AudienceRules.RolesFor(audience).Should().BeEquivalentTo(roles);
}
