using System.Text;
using Ottoman.Infrastructure.Messaging;
using Ottoman.Infrastructure.Notifications;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Ottoman.Worker.Messaging;

/// <summary>
/// Reads events from the notification queue and hands them to <see cref="EventProcessor"/> (e-mail + inbox).
/// A failing message is retried a few times with a growing delay, then dead-lettered for a human to look at.
/// </summary>
public sealed class NotificationConsumer(
    IServiceScopeFactory scopes,
    RabbitConnection rabbit,
    ILogger<NotificationConsumer> log) : BackgroundService
{
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The shared connection of this process; only the channel is ours to close.
        var connection = await rabbit.GetAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await RabbitTopology.DeclareAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, 10, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => HandleAsync(channel, ea, stoppingToken);
        await channel.BasicConsumeAsync(RabbitTopology.NotificationsQueue, false, consumer, stoppingToken);
        log.LogInformation("Notification consumer started");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var type = ea.BasicProperties.Type ?? ea.RoutingKey;
        try
        {
            if (!long.TryParse(ea.BasicProperties.MessageId, out var outboxId))
                throw new InvalidOperationException("Message has no numeric MessageId.");

            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<EventProcessor>()
                .ProcessAsync(outboxId, type, Encoding.UTF8.GetString(ea.Body.Span), ct);
            await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RetryOrParkAsync(channel, ea, type, ex, ct);
        }
    }

    private async Task RetryOrParkAsync(IChannel channel, BasicDeliverEventArgs ea, string type, Exception ex, CancellationToken ct)
    {
        var attempts = ea.BasicProperties.Headers?.TryGetValue(RabbitTopology.AttemptsHeader, out var v) == true && v is int n ? n : 0;
        attempts++;

        if (attempts >= MaxAttempts)
        {
            log.LogError(ex, "Event {Type} failed {Attempts} times; moving to the dead-letter queue", type, attempts);
            await channel.BasicNackAsync(ea.DeliveryTag, false, false, ct);
            return;
        }

        log.LogWarning(ex, "Event {Type} failed (attempt {Attempts}/{Max}); retrying", type, attempts, MaxAttempts);
        // Exponential backoff: 1 s, 2 s, 4 s, 8 s between the five attempts.
        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts - 1)), ct);

        var props = new BasicProperties
        {
            MessageId = ea.BasicProperties.MessageId,
            Type = ea.BasicProperties.Type,
            ContentType = ea.BasicProperties.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?> { [RabbitTopology.AttemptsHeader] = attempts },
        };
        // Back onto the notification queue only (not the exchange), so no other queue ever sees a duplicate.
        await channel.BasicPublishAsync("", RabbitTopology.NotificationsQueue, false, props, ea.Body, ct);
        await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
    }
}
