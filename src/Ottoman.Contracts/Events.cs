namespace Ottoman.Contracts;

/// <summary>Integration events published through the outbox to RabbitMQ.</summary>
public static class Exchanges
{
    public const string Events = "ottoman.events";
}

public record UserRegistered(string UserId, string Email, string FullName);

/// <summary>Someone tried to register with an email that already has an account.</summary>
public record RegistrationAttemptedForExistingAccount(string Email, string FullName);

/// <summary>The reset code travels sealed (see IResetTokenVault): it is never stored in the outbox as plain text.</summary>
public record PasswordResetRequested(string UserId, string Email, string FullName, string SealedToken);

/// <summary>
/// A reset was asked for an address with no (active) account. Carries nothing and sends nothing: it exists so that
/// request does the same work (a sealed code and an outbox write) as a real one and cannot be told apart by timing.
/// </summary>
public record PasswordResetIgnored;

/// <param name="Confirmed">False when the salon reviews bookings by hand: the booking waits for a barber to confirm it.</param>
public record ReservationCreated(int ReservationId, string ClientId, string EmployeeId, DateTime StartUtc, DateTime EndUtc, decimal TotalPrice, bool Confirmed = true);

/// <summary>A barber or the admin confirmed a booking that was waiting (only when the salon reviews bookings by hand).</summary>
public record ReservationConfirmed(int ReservationId, string ClientId, string EmployeeId, DateTime StartUtc, string ConfirmedBy);

/// <param name="CancelledBy">"Client", "Employee" or "Admin".</param>
/// <param name="Declined">The salon turned down a booking it had not confirmed (the reason is always given).</param>
public record ReservationCancelled(int ReservationId, string ClientId, string EmployeeId, DateTime StartUtc, string CancelledBy, string? Reason, bool Declined = false);

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
