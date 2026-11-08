namespace Ottoman.Domain.Scheduling;

public record SlotQuery(
    DateOnly Date,
    TimeZoneInfo Zone,
    WorkingDay? Working,
    IReadOnlyList<TimeRange> Busy,
    int DurationMinutes,
    int StepMinutes,
    int OfferGridMinutes,
    int MinLeadMinutes,
    DateTime NowUtc,
    int ShortestServiceMinutes);

/// <summary>
/// Pure availability logic. All times are UTC internally; only the working day and the offer
/// grid are interpreted in the salon's local time.
/// </summary>
public static class SlotCalculator
{
    /// <summary>Free windows of the day: opening hours minus breaks and busy intervals, merged and ordered.</summary>
    public static IReadOnlyList<TimeRange> FreeWindows(SlotQuery q)
    {
        if (q.Working is not { } day) return [];

        var open = new TimeRange(LocalTime.ToUtc(q.Date, day.Start, q.Zone), LocalTime.ToUtc(q.Date, day.End, q.Zone));
        if (open.IsEmpty) return [];

        var blocked = new List<TimeRange>(q.Busy);
        if (day.BreakStart is { } bs && day.BreakEnd is { } be)
            blocked.Add(new TimeRange(LocalTime.ToUtc(q.Date, bs, q.Zone), LocalTime.ToUtc(q.Date, be, q.Zone)));

        var free = new List<TimeRange>();
        var cursor = open.Start;
        foreach (var b in blocked.Where(b => !b.IsEmpty && b.Overlaps(open)).OrderBy(b => b.Start))
        {
            if (b.Start > cursor) free.Add(new TimeRange(cursor, b.Start));
            if (b.End > cursor) cursor = b.End;
        }
        if (cursor < open.End) free.Add(new TimeRange(cursor, open.End));
        return free;
    }

    /// <summary>Every start at which the whole service fits. This is the "show all times" list.</summary>
    public static IReadOnlyList<DateTime> AllStarts(SlotQuery q) =>
        Candidates(q).Select(c => c.Start).ToList();

    /// <summary>
    /// The starts offered by default. A start is offered if it is on the local offer grid, or flush with
    /// the beginning or end of a free window, and it does not leave a leftover gap shorter than the
    /// shortest service (a gap of exactly zero is fine).
    /// </summary>
    public static IReadOnlyList<DateTime> OfferedStarts(SlotQuery q)
    {
        var duration = TimeSpan.FromMinutes(q.DurationMinutes);
        var shortest = TimeSpan.FromMinutes(Math.Max(1, q.ShortestServiceMinutes));

        return Candidates(q)
            .Where(c =>
            {
                var flushStart = c.Start == c.Window.Start;
                var flushEnd = c.Start + duration == c.Window.End;
                var onGrid = IsOnLocalGrid(c.Start, q);
                if (!(onGrid || flushStart || flushEnd)) return false;

                var before = c.Start - c.Window.Start;
                var after = c.Window.End - (c.Start + duration);
                return GapOk(before, shortest) && GapOk(after, shortest);
            })
            .Select(c => c.Start)
            .ToList();
    }

    private static bool GapOk(TimeSpan gap, TimeSpan shortest) => gap == TimeSpan.Zero || gap >= shortest;

    private readonly record struct Candidate(DateTime Start, TimeRange Window);

    private static IEnumerable<Candidate> Candidates(SlotQuery q)
    {
        if (q.DurationMinutes <= 0 || q.StepMinutes <= 0) yield break;

        var duration = TimeSpan.FromMinutes(q.DurationMinutes);
        var step = TimeSpan.FromMinutes(q.StepMinutes);
        var earliest = q.NowUtc.AddMinutes(q.MinLeadMinutes);
        // The same limit booking enforces (BookingPolicy.Validate): nothing is offered that could not be booked.
        var latest = q.NowUtc.AddDays(Reservations.BookingPolicy.MaxAdvanceDays);

        foreach (var window in FreeWindows(q))
        {
            // First start on the local step grid at or after the window start.
            var start = AlignUp(window.Start, q);
            while (start + duration <= window.End)
            {
                if (start > latest) yield break;
                if (start >= earliest) yield return new Candidate(start, window);
                start += step;
            }
        }
    }

    /// <summary>Rounds an instant up to the next local step boundary (relative to local midnight).</summary>
    private static DateTime AlignUp(DateTime utc, SlotQuery q)
    {
        var local = LocalTime.ToLocal(utc, q.Zone);
        var minutes = (int)local.TimeOfDay.TotalMinutes;
        var remainder = minutes % q.StepMinutes;
        var needsRound = remainder != 0 || local.TimeOfDay.Seconds != 0 || local.Millisecond != 0;
        if (!needsRound) return utc;
        return utc.AddMinutes(q.StepMinutes - remainder).AddSeconds(-local.Second).AddMilliseconds(-local.Millisecond);
    }

    private static bool IsOnLocalGrid(DateTime utc, SlotQuery q)
    {
        var local = LocalTime.ToLocal(utc, q.Zone);
        return q.OfferGridMinutes > 0 && (int)local.TimeOfDay.TotalMinutes % q.OfferGridMinutes == 0;
    }
}
