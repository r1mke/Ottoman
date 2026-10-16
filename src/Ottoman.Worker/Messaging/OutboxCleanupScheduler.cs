using Ottoman.Infrastructure.Outbox;

namespace Ottoman.Worker.Messaging;

/// <summary>Hourly housekeeping of delivered outbox rows (see <see cref="OutboxCleanup"/>).</summary>
public sealed class OutboxCleanupScheduler(IServiceScopeFactory scopes, ILogger<OutboxCleanupScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var removed = await scope.ServiceProvider.GetRequiredService<OutboxCleanup>().PurgeAsync(stoppingToken);
                if (removed > 0) log.LogInformation("Purged {Count} delivered outbox row(s)", removed);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Outbox cleanup failed; will retry");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
