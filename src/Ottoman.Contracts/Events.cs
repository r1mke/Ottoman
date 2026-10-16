namespace Ottoman.Contracts;

/// <summary>Integration events published through the outbox to RabbitMQ.</summary>
public static class Exchanges
{
    public const string Events = "ottoman.events";
}

public record UserRegistered(string UserId, string Email, string FullName);

/// <summary>Someone tried to register with an email that already has an account.</summary>
public record RegistrationAttemptedForExistingAccount(string Email, string FullName);

public record PasswordResetRequested(string UserId, string Email, string FullName, string Token);

public record ReservationCreated(int ReservationId, string ClientId, string EmployeeId, DateTime StartUtc, DateTime EndUtc, decimal TotalPrice);

/// <param name="CancelledBy">"Client", "Employee" or "Admin".</param>
public record ReservationCancelled(int ReservationId, string ClientId, string EmployeeId, DateTime StartUtc, string CancelledBy, string? Reason);

public record ReservationRescheduled(int ReservationId, string ClientId, string EmployeeId, DateTime OldStartUtc, DateTime NewStartUtc);

/// <param name="Method">"Card" or "OnReceipt".</param>
public record OrderPlaced(int OrderId, string ClientId, decimal TotalPrice, string Method);

public record OrderPaid(int OrderId, string ClientId, decimal TotalPrice);

/// <param name="CancelledBy">"Client", "Admin" or "System" (unpaid order expired).</param>
public record OrderCancelled(int OrderId, string ClientId, string CancelledBy);

public record ProductLowStock(int ProductId, string Name, int Stock, int Threshold);

/// <param name="Kind">"Day" or "Hours".</param>
public record ReservationReminderDue(int ReservationId, string ClientId, string EmployeeId, DateTime StartUtc, string Kind);

public record AnnouncementPublished(int AnnouncementId);
