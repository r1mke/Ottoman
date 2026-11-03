namespace Ottoman.Application.Payments;

public class PaymentOptions
{
    public const string Section = "Payments";

    /// <summary>Stripe secret key. Empty = no processor configured (card orders can be created but never get paid).</summary>
    public string SecretKey { get; set; } = "";

    /// <summary>Publishable key (pk_...). Safe to hand to the apps; they need it to open the card sheet.</summary>
    public string PublishableKey { get; set; } = "";

    /// <summary>Signing secret of the webhook endpoint. Empty = the webhook endpoint refuses everything.</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Stripe cannot charge in BAM, so card payments are taken in EUR at the fixed peg.</summary>
    public decimal BamPerEur { get; set; } = Ottoman.Domain.Orders.OrderPricing.DefaultBamPerEur;

    public string Currency { get; set; } = "eur";

    /// <summary>How long an unpaid card order holds its stock.</summary>
    public int PendingOrderMinutes { get; set; } = 30;
}

public record PaymentIntentResult(string ProviderRef, string ClientSecret);

/// <summary>The card processor behind a port, so the shop logic is testable without network calls.</summary>
public interface IPaymentGateway
{
    Task<PaymentIntentResult> CreateIntentAsync(int orderId, long amountMinor, string currency, CancellationToken ct);
    Task RefundAsync(string providerRef, CancellationToken ct);
    Task CancelIntentAsync(string providerRef, CancellationToken ct);
}

public enum PaymentEventKind
{
    Succeeded,
    Failed,
    Ignored,
}

/// <summary>What the webhook tells us, reduced to what matters. Built only from a verified signature.</summary>
public record PaymentEvent(string EventId, PaymentEventKind Kind, string ProviderRef, long AmountMinor, string Currency);

public enum WebhookOutcome
{
    Processed,
    /// <summary>Signature missing/invalid/expired, or the endpoint is not configured.</summary>
    Unauthorized,
    /// <summary>Authentic but wrong (e.g. amount differs from the order); nothing was changed.</summary>
    Rejected,
    /// <summary>Recorded, but a follow-up (a refund) did not go through yet; answered non-2xx so the processor retries.</summary>
    RetryLater,
}

public interface IPaymentWebhookHandler
{
    Task<WebhookOutcome> HandleAsync(string payload, string? signatureHeader, CancellationToken ct);
}
