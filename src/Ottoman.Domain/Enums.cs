namespace Ottoman.Domain;

public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4,
}

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3,
}
