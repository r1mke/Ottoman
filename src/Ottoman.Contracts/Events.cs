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
