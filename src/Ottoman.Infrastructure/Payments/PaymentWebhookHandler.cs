using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ottoman.Application.Common;
using Ottoman.Application.Payments;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Orders;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Payments;

/// <summary>
/// Turns processor webhooks into order state. Idempotent (replays change nothing), amount-checked, and
/// authenticated by signature only: the request carries no user, so nothing in it is trusted before that check.
/// </summary>
public class PaymentWebhookHandler(
    OttomanDbContext db,
    PendingRefunds refunds,
    IOutbox outbox,
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

        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.PaymentId == payment.Id, ct);
        if (order is null) return WebhookOutcome.Rejected;

        if (ev.Kind == PaymentEventKind.Failed)
        {
            await db.Payments.Where(p => p.Id == payment.Id && p.Status == PaymentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed), ct);
            return WebhookOutcome.Processed;
        }

        // The amount that was actually charged must be the amount we asked for. Anything else is never "paid".
        var expectedMinor = OrderPricing.ToMinorUnits(order.TotalPrice, opt.BamPerEur);
        if (ev.AmountMinor != expectedMinor || !string.Equals(ev.Currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            log.LogError("Payment {Ref} amount mismatch: got {Got} {Cur}, expected {Expected} {PayCur} for order {OrderId}",
                ev.ProviderRef, ev.AmountMinor, ev.Currency, expectedMinor, payment.Currency, order.Id);
            return WebhookOutcome.Rejected;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var refundNeeded = false;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            refundNeeded = false;
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                // Claim the payment: only the first delivery of this event gets past here.
                var claimed = await db.Payments.Where(p => p.Id == payment.Id
                        && p.Status != PaymentStatus.Succeeded && p.Status != PaymentStatus.Refunded && p.Status != PaymentStatus.RefundPending)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Succeeded), ct);
                if (claimed == 0)
                {
                    await tx.RollbackAsync(ct);
                    return; // replay
                }

                var moved = await db.Orders.Where(o => o.Id == order.Id && o.Status == OrderStatus.PendingPayment)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Placed).SetProperty(o => o.PaidAtUtc, now), ct);
                if (moved == 1) outbox.Enqueue(new OrderPaid(order.Id, order.ClientId, order.TotalPrice));
                else
                {
                    // Paid after the order was cancelled/expired: the money goes back. The decision is recorded in this
                    // transaction, so a failed refund call below is retried (Stripe's retry, the expiry job), never lost.
                    await db.Payments.Where(p => p.Id == payment.Id)
                        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.RefundPending), ct);
                    refundNeeded = true;
                }

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        // A replay of an event whose refund did not go through the first time retries that refund.
        if (!refundNeeded)
            refundNeeded = await db.Payments.AsNoTracking().AnyAsync(p => p.Id == payment.Id && p.Status == PaymentStatus.RefundPending, ct);

        if (refundNeeded)
        {
            log.LogWarning("Order {OrderId} was no longer awaiting payment when {Ref} succeeded; refunding", order.Id, payment.ProviderRef);
            if (!await refunds.CompleteAsync(payment.Id, payment.ProviderRef, ct))
                return WebhookOutcome.RetryLater; // non-2xx: Stripe delivers the event again
        }

        return WebhookOutcome.Processed;
    }
}
