using Ottoman.Infrastructure.Notifications;

namespace Ottoman.Worker.Messaging;

/// <summary>Every minute, queue reminders for appointments that have just become due.</summary>
public sealed class ReminderScheduler(IServiceScopeFactory scopes, ILogger<ReminderScheduler> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var queued = await scope.ServiceProvider.GetRequiredService<ReminderService>().EnqueueDueAsync(stoppingToken);
                if (queued > 0) log.LogInformation("Queued {Count} reminder(s)", queued);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Reminder scan failed; will retry");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
