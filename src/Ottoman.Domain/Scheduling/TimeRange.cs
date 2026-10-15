namespace Ottoman.Domain.Scheduling;

/// <summary>Half-open UTC interval [Start, End).</summary>
public readonly record struct TimeRange(DateTime Start, DateTime End)
{
    public TimeSpan Duration => End - Start;
    public bool IsEmpty => End <= Start;

    public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;

    public bool Contains(DateTime instant) => instant >= Start && instant < End;
}
