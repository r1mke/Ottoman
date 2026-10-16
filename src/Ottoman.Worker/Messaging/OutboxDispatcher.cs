using Microsoft.EntityFrameworkCore;
using Ottoman.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace Ottoman.Worker.Messaging;

/// <summary>
/// Moves committed outbox rows to RabbitMQ. A row is marked done only after the broker confirmed the publish,
/// so a crash can duplicate an event but never lose one (consumers de-duplicate by the outbox id).
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    RabbitConnection rabbit,
    TimeProvider clock,
    ILogger<OutboxDispatcher> log) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 10;
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await rabbit.ConnectAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);
        await RabbitTopology.DeclareAsync(channel, stoppingToken);
        log.LogInformation("Outbox dispatcher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            int sent;
            try
            {
                sent = await DispatchBatchAsync(channel, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Outbox dispatch failed; retrying");
                sent = 0;
            }

            if (sent < BatchSize)
                await Task.Delay(Idle, stoppingToken);
        }
    }

    private async Task<int> DispatchBatchAsync(IChannel channel, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();

        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.Id).Take(BatchSize).ToListAsync(ct);

        foreach (var m in batch)
        {
            try
            {
                var props = new BasicProperties
                {
                    MessageId = m.Id.ToString(),
                    Type = m.Type,
                    ContentType = "application/json",
                    DeliveryMode = DeliveryModes.Persistent,
                };
                await channel.BasicPublishAsync(
                    RabbitTopology.Exchange, m.Type, mandatory: false, props,
                    System.Text.Encoding.UTF8.GetBytes(m.PayloadJson), ct);
                m.ProcessedAtUtc = clock.GetUtcNow().UtcDateTime;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                m.Attempts++;
                log.LogWarning(ex, "Publishing outbox message {Id} ({Type}) failed, attempt {Attempts}", m.Id, m.Type, m.Attempts);
            }
            await db.SaveChangesAsync(ct);
        }
        return batch.Count;
    }
}
