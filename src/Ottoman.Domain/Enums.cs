using System.Text.Json.Serialization;

namespace Ottoman.Domain;

/// <summary>Only used to pick a default avatar for a barber without a photo.</summary>
public enum Gender
{
    Male = 1,
    Female = 2,
}

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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnnouncementAudience
{
    All = 0,
    Clients = 1,
    Employees = 2,
    User = 3,
}

public enum ReminderKind
{
    /// <summary>About a day before the appointment.</summary>
    Day = 0,
    /// <summary>A couple of hours before.</summary>
    Hours = 1,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReviewTargetType
{
    Employee = 0,
    Service = 1,
    Product = 2,
}
