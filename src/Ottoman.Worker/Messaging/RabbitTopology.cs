using RabbitMQ.Client;

namespace Ottoman.Worker.Messaging;

/// <summary>
/// One topic exchange for all integration events (routing key = event type). The notification queue gets every
/// event; messages that keep failing are dead-lettered into a parking queue instead of being lost or looping forever.
/// </summary>
public static class RabbitTopology
{
    public const string Exchange = Ottoman.Contracts.Exchanges.Events;
    public const string DeadLetterExchange = Exchange + ".dlx";
    public const string NotificationsQueue = "ottoman.notifications";
    public const string NotificationsDeadLetterQueue = "ottoman.notifications.dlq";
    public const string AttemptsHeader = "x-attempts";

    public static async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true, cancellationToken: ct);

        await channel.QueueDeclareAsync(NotificationsDeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await channel.QueueBindAsync(NotificationsDeadLetterQueue, DeadLetterExchange, "", cancellationToken: ct);

        await channel.QueueDeclareAsync(
            NotificationsQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange },
            cancellationToken: ct);
        await channel.QueueBindAsync(NotificationsQueue, Exchange, "#", cancellationToken: ct);
    }
}
