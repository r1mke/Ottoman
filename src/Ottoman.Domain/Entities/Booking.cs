namespace Ottoman.Domain.Entities;

public class Reservation
{
    public int Id { get; set; }
    public string ClientId { get; set; } = null!;
    public string EmployeeId { get; set; } = null!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string? Note { get; set; }

    // Snapshots: stay correct when the catalogue changes later.
    public decimal TotalPrice { get; set; }
    public int TotalDurationMinutes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Who moved it from Pending to Confirmed ("System" when the salon auto-confirms) and when.</summary>
    public DateTime? ConfirmedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }

    /// <summary>Who closed the visit (completed or no-show) and when.</summary>
    public DateTime? CompletedAtUtc { get; set; }
    public string? CompletedBy { get; set; }

    /// <summary>The salon turned the booking down before confirming it (a cancellation with a reason).</summary>
    public bool Declined { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<ReservationItem> Items { get; set; } = [];
    public List<ReservationSlot> Slots { get; set; } = [];
}

public class ReservationItem
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int ServiceId { get; set; }
    public string ServiceName { get; set; } = null!;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
}

/// <summary>
/// One row per occupied booking step. UNIQUE(EmployeeId, SlotStartUtc) lets the
/// database itself reject overlapping reservations of one barber, and UNIQUE(ClientId, SlotStartUtc) rejects a
/// client being booked with two barbers at the same time.
/// </summary>
public class ReservationSlot
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = null!;

    /// <summary>The client the step belongs to. Null only on rows created before this rule existed.</summary>
    public string? ClientId { get; set; }
    public DateTime SlotStartUtc { get; set; }
    public int ReservationId { get; set; }
}
