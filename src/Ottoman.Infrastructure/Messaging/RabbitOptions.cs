namespace Ottoman.Infrastructure.Messaging;

public class RabbitOptions
{
    public const string Section = "RabbitMq";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    /// <summary>Shown in the RabbitMQ management UI next to the connection.</summary>
    public string ClientName { get; set; } = "ottoman";
}
