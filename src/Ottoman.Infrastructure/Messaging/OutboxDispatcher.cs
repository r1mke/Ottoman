using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace Ottoman.Infrastructure.Messaging;

/// <summary>
/// Runs in the API: publishes committed outbox rows to RabbitMQ on one channel of the shared connection, with
/// publisher confirms. A row is marked done only after the broker confirmed it, so a crash can at worst repeat an
/// event (consumers de-duplicate by outbox id) but never lose one. Rows are claimed with UPDLOCK + READPAST inside
/// the batch transaction, so two API instances never publish the same row at the same time.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    RabbitConnection rabbit,
    TimeProvider clock,
    ILogger<OutboxDispatcher> log) : BackgroundService
{
    public const int BatchSize = 50;
    public const int MaxAttempts = 10;
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the host finish starting (and the database migrate) before the first round.
        await Task.Yield();
        IChannel? channel = null;
        try
        {
            var connection = await rabbit.GetAsync(stoppingToken);
            channel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                stoppingToken);
            await RabbitTopology.DeclareAsync(channel, stoppingToken);
            log.LogInformation("Outbox publisher started");

            while (!stoppingToken.IsCancellationRequested)
            {
                int sent;
                try
                {
                    sent = await PublishBatchAsync(channel, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Publishing the outbox failed; retrying");
                    sent = 0;
                }

                if (sent < BatchSize)
                    await Task.Delay(Idle, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
        finally
        {
            if (channel is not null) await channel.DisposeAsync();
        }
    }

    /// <summary>Publishes one batch. Public so tests can drive a round without the timer.</summary>
    public async Task<int> PublishBatchAsync(IChannel channel, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var table = db.Model.FindEntityType(typeof(OutboxMessage))!.GetTableName();
        var claim = "SELECT TOP (" + BatchSize + ") * FROM [" + table + "] WITH (UPDLOCK, READPAST, ROWLOCK) " +
                    "WHERE [ProcessedAtUtc] IS NULL AND [Attempts] < {0} ORDER BY [Id]";

        var count = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var batch = await db.OutboxMessages.FromSqlRaw(claim, MaxAttempts).ToListAsync(ct);
            foreach (var m in batch)
            {
                try
                {
                    var props = new BasicProperties
                    {
                        MessageId = m.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Type = m.Type,
                        ContentType = "application/json",
                        DeliveryMode = DeliveryModes.Persistent,
                    };
                    // With confirmations enabled this completes only once the broker has the message.
                    await channel.BasicPublishAsync(RabbitTopology.Exchange, m.Type, mandatory: false, props,
                        Encoding.UTF8.GetBytes(m.PayloadJson), ct);
                    m.ProcessedAtUtc = clock.GetUtcNow().UtcDateTime;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    m.Attempts++;
                    log.LogWarning(ex, "Publishing outbox message {Id} ({Type}) failed, attempt {Attempts}", m.Id, m.Type, m.Attempts);
                }
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            count = batch.Count;
        });
        return count;
    }
}
