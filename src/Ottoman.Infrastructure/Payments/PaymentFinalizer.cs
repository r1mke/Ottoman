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

public enum FinalizeOutcome
{
    /// <summary>The order is paid now (or already was: a repeat changes nothing).</summary>
    Paid,

    /// <summary>The money came for an order that was already closed; it is (being) refunded.</summary>
    Refunded,

    /// <summary>Recorded, but the refund did not go through yet; retried later.</summary>
    RefundPending,

    /// <summary>The amount or currency is not what the order asked for: nothing changed.</summary>
    Rejected,
}

/// <summary>
/// The one place where a successful card payment turns into order state, used by the webhook and by the app's
/// "confirm payment" call alike. Amount-checked, idempotent (the payment row is claimed atomically, so two
/// deliveries never both act) and refund-safe (a late payment for a closed order is marked RefundPending in the
/// same transaction).
/// </summary>
public class PaymentFinalizer(
    OttomanDbContext db,
    PendingRefunds refunds,
    IOutbox outbox,
    IOptions<PaymentOptions> options,
    TimeProvider clock,
    ILogger<PaymentFinalizer> log)
{
    public async Task<FinalizeOutcome> SucceededAsync(int paymentId, string providerRef, long amountMinor, string currency, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstAsync(p => p.Id == paymentId, ct);
        var order = await db.Orders.AsNoTracking().FirstAsync(o => o.PaymentId == paymentId, ct);

        // The amount that was actually charged must be the amount we asked for. Anything else is never "paid".
        var expectedMinor = OrderPricing.ToMinorUnits(order.TotalPrice, options.Value.BamPerEur);
        if (amountMinor != expectedMinor || !string.Equals(currency, payment.Currency, StringComparison.OrdinalIgnoreCase))
        {
            log.LogError("Payment {Ref} amount mismatch: got {Got} {Cur}, expected {Expected} {PayCur} for order {OrderId}",
                providerRef, amountMinor, currency, expectedMinor, payment.Currency, order.Id);
            return FinalizeOutcome.Rejected;
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
                // Claim the payment: only the first confirmation of it gets past here.
                var claimed = await db.Payments.Where(p => p.Id == paymentId
                        && p.Status != PaymentStatus.Succeeded && p.Status != PaymentStatus.Refunded && p.Status != PaymentStatus.RefundPending)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Succeeded), ct);
                if (claimed == 0)
                {
                    await tx.RollbackAsync(ct);
                    return; // a repeat
                }

                var moved = await db.Orders.Where(o => o.Id == order.Id && o.Status == OrderStatus.PendingPayment)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Placed).SetProperty(o => o.PaidAtUtc, now), ct);
                if (moved == 1) outbox.Enqueue(new OrderPaid(order.Id, order.ClientId, order.TotalPrice));
                else
                {
                    // Paid after the order was cancelled/expired: the money goes back. The decision is recorded in this
                    // transaction, so a failed refund call below is retried (Stripe's retry, the expiry job), never lost.
                    await db.Payments.Where(p => p.Id == paymentId)
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

        var status = await db.Payments.AsNoTracking().Where(p => p.Id == paymentId).Select(p => p.Status).FirstAsync(ct);
        if (status == PaymentStatus.RefundPending || refundNeeded)
        {
            log.LogWarning("Order {OrderId} was no longer awaiting payment when {Ref} succeeded; refunding", order.Id, providerRef);
            return await refunds.CompleteAsync(paymentId, providerRef, ct) ? FinalizeOutcome.Refunded : FinalizeOutcome.RefundPending;
        }
        return status == PaymentStatus.Refunded ? FinalizeOutcome.Refunded : FinalizeOutcome.Paid;
    }
}
