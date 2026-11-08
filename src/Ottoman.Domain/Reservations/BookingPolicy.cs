namespace Ottoman.Domain.Reservations;

/// <summary>Limits that protect the salon from abuse and mistakes. Pure and independent of storage.</summary>
public static class BookingPolicy
{
    public const int MaxAdvanceDays = 90;
    public const int MaxActiveReservationsPerClient = 5;
    public const int MaxServicesPerReservation = 10;
    public const int MaxNoteLength = 500;

    public static readonly string OutsideWindowMessage =
        $"Termini se mogu birati od danas do najviše {MaxAdvanceDays} dana unaprijed.";

    /// <summary>
    /// The calendar dates anyone may ask availability for: from yesterday to <see cref="MaxAdvanceDays"/> + 1 days
    /// ahead of today's UTC date (one day of slack each side, because the salon's day and the UTC day differ).
    /// Which start times inside it are really bookable is decided per instant.
    /// </summary>
    public static (DateOnly First, DateOnly Last) DateWindow(DateOnly todayUtc) =>
        (todayUtc.AddDays(-1), todayUtc.AddDays(MaxAdvanceDays + 1));

    public static bool InDateWindow(DateOnly date, DateOnly todayUtc)
    {
        var (first, last) = DateWindow(todayUtc);
        return date >= first && date <= last;
    }

    /// <summary>A month (any day of it) that shares at least one day with the window.</summary>
    public static bool MonthInDateWindow(DateOnly month, DateOnly todayUtc)
    {
        var (first, last) = DateWindow(todayUtc);
        var start = new DateOnly(month.Year, month.Month, 1);
        return start <= last && start >= new DateOnly(first.Year, first.Month, 1);
    }

    public static IReadOnlyList<string> Validate(
        DateTime startUtc,
        DateTime nowUtc,
        int serviceCount,
        int activeReservationsOfClient,
        string? note,
        bool bypassClientLimit)
    {
        var errors = new List<string>();

        if (serviceCount is < 1 or > MaxServicesPerReservation)
            errors.Add($"Odaberite između 1 i {MaxServicesPerReservation} usluga.");

        if (startUtc > nowUtc.AddDays(MaxAdvanceDays))
            errors.Add($"Termin se može rezervisati najviše {MaxAdvanceDays} dana unaprijed.");

        if (!bypassClientLimit && activeReservationsOfClient >= MaxActiveReservationsPerClient)
            errors.Add($"Imate već {MaxActiveReservationsPerClient} aktivnih rezervacija. Otkažite neku ili sačekajte da prođe.");

        if (note is { Length: > MaxNoteLength })
            errors.Add($"Napomena može imati najviše {MaxNoteLength} znakova.");

        return errors;
    }
}
