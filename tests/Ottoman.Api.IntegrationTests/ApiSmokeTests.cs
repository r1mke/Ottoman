using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Domain;
using Ottoman.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Starts the real API against a real SQL Server (Testcontainers). Requires Docker.</summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync() => await _sql.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // UseSetting (not ConfigureAppConfiguration): Program.cs reads the connection
        // string during service registration, before late config sources are applied.
        builder.UseSetting("ConnectionStrings:Default", _sql.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:DemoData", "true");
    }
}

public class ApiSmokeTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Health_endpoints_respond_ok()
    {
        var client = api.CreateClient();

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Startup_migrates_and_seeds_roles_and_demo_data()
    {
        _ = api.CreateClient(); // forces host start (migration + seed)
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();

        (await db.Roles.Select(r => r.Name).ToListAsync()).Should().BeEquivalentTo(Roles.All);
        (await db.SalonSettings.SingleAsync()).SlotMinutes.Should().Be(5);
        (await db.EmployeeProfiles.CountAsync()).Should().Be(2);
        (await db.Services.CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Database_rejects_overlapping_slot_for_same_employee()
    {
        _ = api.CreateClient();
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var employeeId = (await db.EmployeeProfiles.FirstAsync()).UserId;
        var clientId = (await db.Users.FirstAsync(u => u.Email == "klijent@ottoman.local")).Id;
        var start = new DateTime(2030, 1, 7, 9, 0, 0, DateTimeKind.Utc);

        Ottoman.Domain.Entities.Reservation Make() => new()
        {
            ClientId = clientId,
            EmployeeId = employeeId,
            StartUtc = start,
            EndUtc = start.AddMinutes(30),
            CreatedAtUtc = DateTime.UtcNow,
            Slots = [new() { EmployeeId = employeeId, SlotStartUtc = start }],
        };

        db.Reservations.Add(Make());
        await db.SaveChangesAsync();

        db.Reservations.Add(Make());
        var act = () => db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
