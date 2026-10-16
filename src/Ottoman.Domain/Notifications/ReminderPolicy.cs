namespace Ottoman.Domain.Notifications;

/// <summary>Pure rules for when an appointment reminder is due.</summary>
public static class ReminderPolicy
{
    public static readonly TimeSpan DayBefore = TimeSpan.FromHours(24);
    public static readonly TimeSpan HoursBefore = TimeSpan.FromHours(2);

    /// <summary>
    /// Reminders that should be sent now. The day-before reminder is for appointments that were booked
    /// well ahead (not "see you in 5 hours" for a booking made this morning); each kind is sent at most once.
    /// </summary>
    public static IReadOnlyList<ReminderKind> Due(
        DateTime nowUtc,
        DateTime startUtc,
        DateTime createdAtUtc,
        IReadOnlySet<ReminderKind> alreadySent)
    {
        var untilStart = startUtc - nowUtc;
        if (untilStart <= TimeSpan.Zero) return [];

        var due = new List<ReminderKind>(2);

        if (!alreadySent.Contains(ReminderKind.Day)
            && untilStart <= DayBefore && untilStart > HoursBefore
            && startUtc - createdAtUtc >= DayBefore)
            due.Add(ReminderKind.Day);

        if (!alreadySent.Contains(ReminderKind.Hours)
            && untilStart <= HoursBefore
            && startUtc - createdAtUtc >= HoursBefore)
            due.Add(ReminderKind.Hours);

        return due;
    }
}
