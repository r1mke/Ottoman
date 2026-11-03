using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Messaging;
using Ottoman.Infrastructure.Persistence;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Ottoman.Api.IntegrationTests;

/// <summary>The API publishes the outbox to a real RabbitMQ (Testcontainers): once per row, confirmed, marked done.</summary>
[Collection("api")]
public sealed class OutboxPublishingTests(ApiFixture api) : IAsyncLifetime
{
    private const string User = "ottoman";
    private const string Password = "ottoman-test";

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder("rabbitmq:4.3.6-management")
        .WithUsername(User).WithPassword(Password).Build();

    private RabbitConnection? _connection;

    public Task InitializeAsync() => _rabbit.StartAsync();

    public async Task DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
        await _rabbit.DisposeAsync();
    }

    private RabbitConnection Connection()
    {
        var uri = new Uri(_rabbit.GetConnectionString());
        return _connection ??= new RabbitConnection(
            Options.Create(new RabbitOptions { Host = uri.Host, Port = uri.Port, User = User, Password = Password, ClientName = "ottoman-tests" }),
            NullLogger<RabbitConnection>.Instance);
    }

    private OutboxDispatcher Dispatcher(RabbitConnection connection) =>
        ActivatorUtilities.CreateInstance<OutboxDispatcher>(api.Services, connection);

    private static async Task DrainAsync(OutboxDispatcher dispatcher, IChannel channel)
    {
        for (var round = 0; round < 1000; round++)
            if (await dispatcher.PublishBatchAsync(channel, default) == 0) return;
    }

    [Fact]
    public async Task Two_publishers_at_once_put_every_outbox_row_on_the_queue_exactly_once_and_mark_it_done()
    {
        var tag = Guid.NewGuid().ToString("N");
        var ours = new List<OutboxMessage>();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            for (var i = 0; i < 120; i++)
                ours.Add(new OutboxMessage { Type = "TestEvent", PayloadJson = $"{{\"tag\":\"{tag}\",\"n\":{i}}}", CreatedAtUtc = DateTime.UtcNow });
            db.OutboxMessages.AddRange(ours);
            await db.SaveChangesAsync();
        }

        var connection = await Connection().GetAsync(default);
        var confirms = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
        await using var first = await connection.CreateChannelAsync(confirms);
        await using var second = await connection.CreateChannelAsync(confirms);
        await RabbitTopology.DeclareAsync(first, default);
        (await Connection().GetAsync(default)).Should().BeSameAs(connection, "the process shares one connection");

        await Task.WhenAll(DrainAsync(Dispatcher(Connection()), first), DrainAsync(Dispatcher(Connection()), second));

        // Read everything that reached the notification queue.
        await using var reader = await connection.CreateChannelAsync();
        var ids = new List<string>();
        while (await reader.BasicGetAsync(RabbitTopology.NotificationsQueue, autoAck: true) is { } msg)
            ids.Add(msg.BasicProperties.MessageId!);

        ids.Should().OnlyHaveUniqueItems("no outbox row is published twice");
        ids.Should().Contain(ours.Select(m => m.Id.ToString()));

        await using var check = api.Services.CreateAsyncScope();
        var rows = await check.ServiceProvider.GetRequiredService<OttomanDbContext>().OutboxMessages.AsNoTracking()
            .Where(m => m.PayloadJson.Contains(tag)).ToListAsync();
        rows.Should().HaveCount(120).And.OnlyContain(m => m.ProcessedAtUtc != null);
    }

    [Fact]
    public void The_api_registers_the_publisher_unless_tests_switch_it_off()
    {
        // The test host switched it off, so nothing here talks to a RabbitMQ that may run on the machine.
        api.Services.GetServices<IHostedService>().Should().NotContain(s => s is OutboxDispatcher);
        api.Services.GetRequiredService<RabbitConnection>().Should().NotBeNull("the connection is a singleton of the process");
    }
}
