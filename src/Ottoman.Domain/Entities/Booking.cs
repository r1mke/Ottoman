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

    public int? PaymentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
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
/// database itself reject overlapping reservations.
/// </summary>
public class ReservationSlot
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = null!;
    public DateTime SlotStartUtc { get; set; }
    public int ReservationId { get; set; }
}
