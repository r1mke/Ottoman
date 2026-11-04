namespace Ottoman.Domain.Reservations;

/// <summary>Who is trying to change a reservation.</summary>
public enum ActorKind
{
    Client,
    Employee,
    Admin,

    /// <summary>The server itself, e.g. confirming a booking automatically when the salon does not review them.</summary>
    System,
}

public readonly record struct Transition(bool Allowed, string? Reason = null)
{
    public static Transition Ok { get; } = new(true);
    public static Transition Deny(string reason) => new(false, reason);
}

/// <summary>
/// Pure reservation lifecycle rules: Pending -> Confirmed -> Completed / NoShow, and Cancelled from any open state
/// (from Pending by the salon it is a decline, which needs a reason). Ownership ("is this the client's own
/// reservation?") is checked by the caller; this class only decides whether the status change itself is allowed.
/// </summary>
public static class ReservationStateMachine
{
    /// <summary>
    /// The status a new booking starts in, and whether the system confirms it at once. Every booking is created
    /// Pending (its time is held); when the salon auto-confirms, the system moves it to Confirmed straight away.
    /// </summary>
    public static (ReservationStatus Initial, bool SystemConfirms) OnCreate(bool autoConfirm) =>
        (ReservationStatus.Pending, autoConfirm);

    /// <summary>The salon turns down a booking it has not confirmed yet. The reason is checked by <see cref="CancellationRules"/>.</summary>
    public static Transition CanDecline(ReservationStatus from, ActorKind actor)
    {
        if (actor is not (ActorKind.Employee or ActorKind.Admin)) return Transition.Deny("Samo osoblje može odbiti rezervaciju.");
        return from == ReservationStatus.Pending
            ? Transition.Ok
            : Transition.Deny("Odbiti se može samo rezervacija koja čeka potvrdu.");
    }

    public static Transition Can(
        ReservationStatus from,
        ReservationStatus to,
        ActorKind actor,
        DateTime nowUtc,
        DateTime startUtc,
        int cancelWindowMinutes)
    {
        if (from is ReservationStatus.Completed or ReservationStatus.Cancelled or ReservationStatus.NoShow)
            return Transition.Deny("Rezervacija je već zatvorena i ne može se mijenjati.");

        return to switch
        {
            ReservationStatus.Confirmed => from == ReservationStatus.Pending
                ? Staff(actor, "Samo osoblje može potvrditi rezervaciju.")
                : Transition.Deny("Rezervacija je već potvrđena."),

            ReservationStatus.Cancelled => Cancel(actor, nowUtc, startUtc, cancelWindowMinutes),

            ReservationStatus.Completed or ReservationStatus.NoShow => Finish(from, actor, nowUtc, startUtc),

            _ => Transition.Deny("Nepodržana promjena statusa."),
        };
    }

    private static Transition Cancel(ActorKind actor, DateTime now, DateTime start, int windowMinutes)
    {
        switch (actor)
        {
            case ActorKind.Admin:
                return Transition.Ok; // the salon can always cancel (e.g. barber is ill, even after the start)
            case ActorKind.Employee:
                return now < start ? Transition.Ok : Transition.Deny("Termin je već počeo.");
            default:
                if (now >= start) return Transition.Deny("Termin je već počeo.");
                return start - now >= TimeSpan.FromMinutes(windowMinutes)
                    ? Transition.Ok
                    : Transition.Deny($"Termin se može otkazati najkasnije {FormatWindow(windowMinutes)} prije početka. Kontaktirajte salon.");
        }
    }

    private static Transition Finish(ReservationStatus from, ActorKind actor, DateTime now, DateTime start)
    {
        if (actor == ActorKind.Client) return Transition.Deny("Samo osoblje može završiti termin.");
        if (from != ReservationStatus.Confirmed) return Transition.Deny("Rezervacija mora biti potvrđena.");
        return now >= start ? Transition.Ok : Transition.Deny("Termin još nije počeo.");
    }

    /// <summary>Staff, and the system acting for the salon, may confirm; a client never.</summary>
    private static Transition Staff(ActorKind actor, string reason) =>
        actor == ActorKind.Client ? Transition.Deny(reason) : Transition.Ok;

    private static string FormatWindow(int minutes) =>
        minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes} min";
}
