namespace Ottoman.Domain.Entities;

public class Payment
{
    public int Id { get; set; }
    public string Provider { get; set; } = "Stripe";
    /// <summary>Provider reference (e.g. PaymentIntent id). Unique: one payment can settle only one thing.</summary>
    public string ProviderRef { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BAM";
    public PaymentStatus Status { get; set; }
    /// <summary>Needed by the client to confirm the payment; only ever returned to the order owner.</summary>
    public string? ClientSecret { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class RefreshToken
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? ReplacedByHash { get; set; }
}

/// <summary>Transactional outbox: written with the business change, dispatched to RabbitMQ by the worker.</summary>
public class OutboxMessage
{
    public long Id { get; set; }
    public string Type { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int Attempts { get; set; }
}
