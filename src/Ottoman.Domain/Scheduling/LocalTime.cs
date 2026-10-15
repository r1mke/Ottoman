namespace Ottoman.Domain.Scheduling;

/// <summary>Conversions between the salon's local wall-clock time and UTC, DST-safe.</summary>
public static class LocalTime
{
    /// <summary>
    /// Local wall time to UTC. A time that does not exist (spring-forward gap) is moved forward by the
    /// size of the gap; an ambiguous time (fall-back) resolves to its first occurrence.
    /// </summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // Shift to the first valid instant after the gap.
            var probe = local;
            while (zone.IsInvalidTime(probe)) probe = probe.AddMinutes(1);
            local = probe;
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            var first = offsets.Max(); // larger offset = earlier UTC instant = first occurrence
            return DateTime.SpecifyKind(local - first, DateTimeKind.Utc);
        }

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, zone), DateTimeKind.Utc);
    }

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

    /// <summary>The UTC range covering the whole local calendar day (23 or 25 hours on DST change days).</summary>
    public static TimeRange DayRangeUtc(DateOnly date, TimeZoneInfo zone) =>
        new(ToUtc(date, TimeOnly.MinValue, zone), ToUtc(date.AddDays(1), TimeOnly.MinValue, zone));
}
