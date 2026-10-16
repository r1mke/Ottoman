namespace Ottoman.Domain.Reservations;

/// <summary>
/// A client may cancel without explaining. When the salon cancels or rejects a booking the client is owed a reason
/// (it goes into the notification and the record), so staff cannot do it without one.
/// </summary>
public static class CancellationRules
{
    public const int MinReasonLength = 3;
    public const int MaxReasonLength = 300;

    /// <summary>The problem in Bosnian, or null when the reason is acceptable.</summary>
    public static string? ReasonProblem(ActorKind actor, string? reason)
    {
        if (actor == ActorKind.Client) return null;
        return reason is { } r && r.Trim().Length >= MinReasonLength
            ? null
            : $"Unesite razlog otkazivanja (najmanje {MinReasonLength} znaka). Klijent ga dobija u obavijesti.";
    }
}
