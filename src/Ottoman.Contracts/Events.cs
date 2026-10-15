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
