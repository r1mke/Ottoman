using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ottoman.Application.Payments;
using Ottoman.Domain;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Payments;

/// <summary>
/// Completes refunds that were decided (payment marked <see cref="PaymentStatus.RefundPending"/> in the same
/// transaction that closed the order or rejected a late payment) but have not gone through at the processor yet.
/// Safe to run repeatedly and concurrently: the processor call carries an idempotency key and the final status
/// change is conditional.
/// </summary>
public class PendingRefunds(OttomanDbContext db, IPaymentGateway gateway, ILogger<PendingRefunds> log)
{
    private const int BatchSize = 50;

    /// <summary>Refunds one payment. Returns false (and leaves it pending for a later retry) if the processor fails.</summary>
    public async Task<bool> CompleteAsync(int paymentId, string providerRef, CancellationToken ct)
    {
        try
        {
            await gateway.RefundAsync(providerRef, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Refund of payment {PaymentId} ({Ref}) failed; it stays pending and will be retried", paymentId, providerRef);
            return false;
        }

        await db.Payments.Where(p => p.Id == paymentId && p.Status == PaymentStatus.RefundPending)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Refunded), ct);
        return true;
    }

    /// <summary>Retries every pending refund (oldest first, bounded batch). Returns how many went through.</summary>
    public async Task<int> RetryAllAsync(CancellationToken ct)
    {
        var due = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.RefundPending)
            .OrderBy(p => p.Id).Take(BatchSize)
            .Select(p => new { p.Id, p.ProviderRef })
            .ToListAsync(ct);

        var done = 0;
        foreach (var p in due)
            if (await CompleteAsync(p.Id, p.ProviderRef, ct)) done++;
        return done;
    }
}
