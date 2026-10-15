namespace Ottoman.Domain.Reservations;

/// <summary>Limits that protect the salon from abuse and mistakes. Pure and independent of storage.</summary>
public static class BookingPolicy
{
    public const int MaxAdvanceDays = 90;
    public const int MaxActiveReservationsPerClient = 5;
    public const int MaxServicesPerReservation = 10;
    public const int MaxNoteLength = 500;

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
