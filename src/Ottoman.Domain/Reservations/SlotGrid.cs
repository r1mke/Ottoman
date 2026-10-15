namespace Ottoman.Domain.Reservations;

public static class SlotGrid
{
    /// <summary>
    /// The booking steps an appointment occupies: one entry per step from <paramref name="startUtc"/>
    /// (inclusive) up to its end (exclusive). These become <c>ReservationSlot</c> rows whose unique
    /// index makes the database reject overlapping bookings.
    /// </summary>
    public static IReadOnlyList<DateTime> Occupied(DateTime startUtc, int durationMinutes, int stepMinutes)
    {
        if (durationMinutes <= 0 || stepMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Duration and step must be positive.");
        if (durationMinutes % stepMinutes != 0)
            throw new ArgumentException($"Duration {durationMinutes} is not a multiple of the {stepMinutes}-minute step.");

        var slots = new List<DateTime>(durationMinutes / stepMinutes);
        for (var offset = 0; offset < durationMinutes; offset += stepMinutes)
            slots.Add(DateTime.SpecifyKind(startUtc.AddMinutes(offset), DateTimeKind.Utc));
        return slots;
    }
}
