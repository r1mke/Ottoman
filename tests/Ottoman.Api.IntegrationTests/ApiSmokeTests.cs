using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class ApiSmokeTests(ApiFixture api)
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
        (await db.EmployeeProfiles.CountAsync(p => p.UserId != null)).Should().BeGreaterThanOrEqualTo(2); // shared DB: other tests add employees
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
        var start = new DateTime(2035, 6, 4, 9, 0, 0, DateTimeKind.Utc); // far from dates used by availability tests

        Reservation Make() => new()
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
