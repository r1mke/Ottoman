using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ottoman.Application.Orders;

namespace Ottoman.Infrastructure.Orders;

/// <summary>
/// Returns stock held by unpaid card orders and finishes refunds that failed earlier. Runs inside the API process for now (a single salon, one
/// instance); the conditional status change in <see cref="IOrderService.ExpireUnpaidAsync"/> keeps it safe
/// if several instances ever run it at once. It moves to the Worker with the other schedulers in Phase 5.
/// </summary>
public class OrderExpiryService(IServiceScopeFactory scopes, ILogger<OrderExpiryService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                var closed = await orders.ExpireUnpaidAsync(stoppingToken);
                if (closed > 0) log.LogInformation("Expired {Count} unpaid order(s)", closed);
                var refunded = await orders.RetryPendingRefundsAsync(stoppingToken);
                if (refunded > 0) log.LogInformation("Completed {Count} pending refund(s)", refunded);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Expiring unpaid orders failed; will retry");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
