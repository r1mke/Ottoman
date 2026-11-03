using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Starts the real API against a real SQL Server (Testcontainers). Requires Docker.</summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@ottoman.test";
    public const string AdminPassword = "Admin_Test123!";
    public const string DemoPassword = "Test123!";
    // 32 bytes with some variety: the data protection keys of the test database are encrypted with it.
    public static readonly string KeyEncryptionSecret = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)(i * 7)).ToArray());
    public const string WebhookSecret = "whsec_integration_tests";

    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString => _sql.GetConnectionString();

    public async Task InitializeAsync() => await _sql.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // UseSetting (not ConfigureAppConfiguration): Program.cs reads some values during
        // service registration, before late config sources are applied.
        builder.UseSetting("ConnectionStrings:Default", _sql.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:DemoData", "true");
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789-abcdef");
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.UseSetting("Payments:WebhookSecret", WebhookSecret);
        builder.UseSetting("Cloudinary:CloudName", "testcloud");
        builder.UseSetting("Cors:Origins:0", "https://admin.example.com");
        builder.UseSetting("DataProtection:KeyEncryptionSecret", KeyEncryptionSecret);
        // Tests drive the outbox by hand; the API must not publish to whatever RabbitMQ happens to run locally.
        builder.UseSetting(Ottoman.Infrastructure.DependencyInjection.PublishOutboxSetting, "false");
        builder.ConfigureServices(s =>
        {
            // The API does not send mail (the worker does); tests drive the same handler with a recording sender.
            s.AddSingleton<FakeEmailSender>();
            s.AddScoped<Ottoman.Infrastructure.Notifications.EventProcessor>();
            s.AddSingleton<Ottoman.Application.Notifications.IEmailSender>(sp => sp.GetRequiredService<FakeEmailSender>());
        });
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture>;
