using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Ottoman.Infrastructure.Messaging;

/// <summary>
/// The one RabbitMQ connection of this process (registered as a singleton). Publishers and consumers open their
/// own channels on it. It is opened on first use, retrying while the broker is still starting, and recovers by
/// itself after a broker restart (automatic recovery keeps the same connection object).
/// </summary>
public sealed class RabbitConnection(IOptions<RabbitOptions> options, ILogger<RabbitConnection> log) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { } open) return open;
        await _gate.WaitAsync(ct);
        try
        {
            return _connection ??= await ConnectAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IConnection> ConnectAsync(CancellationToken ct)
    {
        var o = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.Host,
            Port = o.Port,
            UserName = o.User,
            Password = o.Password,
            AutomaticRecoveryEnabled = true,
        };

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await factory.CreateConnectionAsync(o.ClientName, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 20));
                log.LogWarning("RabbitMQ not reachable ({Message}); retrying in {Delay}s", ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is { } c)
        {
            try
            {
                await c.CloseAsync();
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "Closing the RabbitMQ connection failed");
            }
            c.Dispose();
        }
        _gate.Dispose();
    }
}
