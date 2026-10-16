using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Ottoman.Worker.Messaging;

/// <summary>Opens connections to the broker, retrying while it is still starting up.</summary>
public class RabbitConnection(IOptions<RabbitOptions> options, ILogger<RabbitConnection> log)
{
    public async Task<IConnection> ConnectAsync(CancellationToken ct)
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
                return await factory.CreateConnectionAsync("ottoman-worker", ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 20));
                log.LogWarning("RabbitMQ not reachable ({Message}); retrying in {Delay}s", ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }
}
