namespace Ottoman.Domain.Scheduling;

/// <summary>One employee's working hours for a weekday, in the salon's local time.</summary>
public record WorkingDay(TimeOnly Start, TimeOnly End, TimeOnly? BreakStart = null, TimeOnly? BreakEnd = null)
{
    /// <summary>Returns the reasons this day is not a valid configuration (empty = valid).</summary>
    public IReadOnlyList<string> Validate(int stepMinutes)
    {
        var errors = new List<string>();
        if (End <= Start) errors.Add("Kraj radnog vremena mora biti poslije početka.");
        if (!OnStep(Start, stepMinutes) || !OnStep(End, stepMinutes))
            errors.Add($"Radno vrijeme mora biti u koracima od {stepMinutes} minuta.");

        if (BreakStart.HasValue != BreakEnd.HasValue)
            errors.Add("Pauza mora imati i početak i kraj.");
        else if (BreakStart is { } bs && BreakEnd is { } be)
        {
            if (be <= bs) errors.Add("Kraj pauze mora biti poslije početka pauze.");
            if (bs < Start || be > End) errors.Add("Pauza mora biti unutar radnog vremena.");
            if (!OnStep(bs, stepMinutes) || !OnStep(be, stepMinutes))
                errors.Add($"Pauza mora biti u koracima od {stepMinutes} minuta.");
        }
        return errors;
    }

    private static bool OnStep(TimeOnly t, int step) => t.Second == 0 && t.Minute % step == 0;
}
