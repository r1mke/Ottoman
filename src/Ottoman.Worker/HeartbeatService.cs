namespace Ottoman.Worker;

public sealed class HeartbeatService(ILogger<HeartbeatService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        log.LogInformation("Ottoman.Worker started");
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            log.LogDebug("Worker alive");
    }
}
