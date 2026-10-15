using System.Text.Json.Serialization;

namespace Ottoman.Domain;

public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    /// <summary>Card order waiting for the payment; stock is held until <c>ExpiresUtc</c>.</summary>
    PendingPayment = 0,
    /// <summary>Paid by card, or accepted for payment at pickup/delivery.</summary>
    Placed = 1,
    Fulfilled = 2,
    Cancelled = 3,
    /// <summary>Card payment never arrived in time; stock was released.</summary>
    Expired = 4,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FulfilmentType
{
    Pickup = 0,
    Delivery = 1,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMethod
{
    Card = 0,
    OnReceipt = 1,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3,
}
