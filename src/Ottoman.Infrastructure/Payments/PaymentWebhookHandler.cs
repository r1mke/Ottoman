using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ottoman.Application.Payments;
using Ottoman.Domain;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Payments;

/// <summary>
/// Turns processor webhooks into order state. Idempotent (replays change nothing), amount-checked, and
/// authenticated by signature only: the request carries no user, so nothing in it is trusted before that check.
/// The state change itself is <see cref="PaymentFinalizer"/>, shared with the app's "confirm payment" call.
/// </summary>
public class PaymentWebhookHandler(
    OttomanDbContext db,
    PaymentFinalizer finalizer,
    IOptions<PaymentOptions> options,
    TimeProvider clock,
    ILogger<PaymentWebhookHandler> log) : IPaymentWebhookHandler
{
    public async Task<WebhookOutcome> HandleAsync(string payload, string? signatureHeader, CancellationToken ct)
    {
        var opt = options.Value;
        if (!StripeWebhook.VerifySignature(payload, signatureHeader, opt.WebhookSecret, clock.GetUtcNow()))
        {
            log.LogWarning("Payment webhook rejected: bad or missing signature");
            return WebhookOutcome.Unauthorized;
        }

        var ev = StripeWebhook.Parse(payload);
        if (ev is null) return WebhookOutcome.Rejected;
        if (ev.Kind == PaymentEventKind.Ignored) return WebhookOutcome.Processed;

        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.ProviderRef == ev.ProviderRef, ct);
        if (payment is null)
        {
            // Signed but unknown (e.g. another product on the same Stripe account). Acknowledge so it is not retried.
            log.LogInformation("Webhook {EventId} refers to unknown payment {Ref}; ignored", ev.EventId, ev.ProviderRef);
            return WebhookOutcome.Processed;
        }

        if (!await db.Orders.AsNoTracking().AnyAsync(o => o.PaymentId == payment.Id, ct)) return WebhookOutcome.Rejected;

        if (ev.Kind == PaymentEventKind.Failed)
        {
            await db.Payments.Where(p => p.Id == payment.Id && p.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed), ct);
            return WebhookOutcome.Processed;
        }

        return await finalizer.SucceededAsync(payment.Id, payment.ProviderRef, ev.AmountMinor, ev.Currency, ct) switch
        {
            FinalizeOutcome.Rejected => WebhookOutcome.Rejected,
            FinalizeOutcome.RefundPending => WebhookOutcome.RetryLater, // non-2xx: Stripe delivers the event again
            _ => WebhookOutcome.Processed,
        };
    }
}
