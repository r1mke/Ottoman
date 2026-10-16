namespace Ottoman.Domain.Reports;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum Granularity
{
    Day,
    Week,
    Month,
}

/// <summary>Pure helpers for reports. Dates are salon-local calendar dates, ranges are inclusive on both ends.</summary>
public static class ReportMath
{
    public const int MaxRangeDays = 800;

    public static int DaysIn(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;

    /// <summary>Returns an error text, or null when the range is usable.</summary>
    public static string? ValidateRange(DateOnly from, DateOnly to)
    {
        if (to < from) return "Završni datum ne može biti prije početnog.";
        if (DaysIn(from, to) > MaxRangeDays) return $"Period ne može biti duži od {MaxRangeDays} dana.";
        return null;
    }

    /// <summary>The period of the same length directly before this one, used for "vs previous period".</summary>
    public static (DateOnly From, DateOnly To) PreviousPeriod(DateOnly from, DateOnly to)
    {
        var length = DaysIn(from, to);
        return (from.AddDays(-length), from.AddDays(-1));
    }

    /// <summary>Daily bars for a month, weekly for half a year, monthly beyond that.</summary>
    public static Granularity AutoGranularity(DateOnly from, DateOnly to) => DaysIn(from, to) switch
    {
        <= 31 => Granularity.Day,
        <= 190 => Granularity.Week,
        _ => Granularity.Month,
    };

    /// <summary>First day of the bucket containing <paramref name="date"/>. Weeks start on Monday.</summary>
    public static DateOnly BucketStart(DateOnly date, Granularity g) => g switch
    {
        Granularity.Day => date,
        Granularity.Week => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        _ => new DateOnly(date.Year, date.Month, 1),
    };

    /// <summary>Every bucket that overlaps the range, including empty ones, so charts have no gaps.</summary>
    public static IReadOnlyList<DateOnly> Buckets(DateOnly from, DateOnly to, Granularity g)
    {
        var result = new List<DateOnly>();
        for (var b = BucketStart(from, g); b <= to; b = Next(b, g)) result.Add(b);
        return result;
    }

    private static DateOnly Next(DateOnly bucket, Granularity g) => g switch
    {
        Granularity.Day => bucket.AddDays(1),
        Granularity.Week => bucket.AddDays(7),
        _ => bucket.AddMonths(1),
    };

    public static string Label(DateOnly bucket, Granularity g) => g switch
    {
        Granularity.Day => $"{bucket.Day:00}.{bucket.Month:00}.",
        Granularity.Week => $"{bucket.Day:00}.{bucket.Month:00}.",
        _ => $"{bucket.Month:00}/{bucket.Year}",
    };

    /// <summary>Change in percent; null when there was nothing before to compare with (avoids "infinite growth").</summary>
    public static double? PercentChange(decimal current, decimal previous)
    {
        if (previous == 0) return current == 0 ? 0 : null;
        return Math.Round((double)((current - previous) / previous * 100m), 1);
    }

    /// <summary>Share in percent, 0 when the whole is empty.</summary>
    public static double Rate(int part, int whole) => whole == 0 ? 0 : Math.Round(part * 100.0 / whole, 1);

    public static decimal Average(decimal total, int count) => count == 0 ? 0 : Math.Round(total / count, 2);
}
