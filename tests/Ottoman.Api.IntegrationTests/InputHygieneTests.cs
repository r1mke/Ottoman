using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ottoman.Application.Auth;
using Ottoman.Application.Employees;
using Ottoman.Application.Shop;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Emails stored in lower case, control and bidi characters refused, expected DB collisions not logged as errors.</summary>
[Collection("api")]
public class InputHygieneTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    [Fact]
    public async Task Emails_are_stored_in_lower_case_and_any_case_still_logs_in()
    {
        var local = Guid.NewGuid().ToString("N")[..10];
        var mixed = $"Ana.{local}@Ottoman.TEST";
        var anon = api.CreateClient();
        (await anon.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Ana", "Anić", "  " + mixed + " ", null, TestClient.StrongPassword))).EnsureSuccessStatusCode();

        var auth = await api.CreateClient().LoginOk(mixed.ToUpperInvariant(), TestClient.StrongPassword);
        auth.User.Email.Should().Be(mixed.ToLowerInvariant());
        (await api.CreateClient().LoginOk(mixed.ToLowerInvariant(), TestClient.StrongPassword)).User.Id.Should().Be(auth.User.Id);

        // Registering again in another case is the same account (no second one).
        (await anon.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Ana", "Anić", mixed.ToUpperInvariant(), null, TestClient.StrongPassword))).EnsureSuccessStatusCode();
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.Users.CountAsync(u => u.NormalizedEmail == mixed.ToUpperInvariant())).Should().Be(1);

        var barber = await (await Admin()).PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "Edin", "Brijač", $"Edin.{local}@Ottoman.Test", null, "Emp_Pass123!", "Barber", null, null));
        barber.StatusCode.Should().Be(HttpStatusCode.Created);
        (await barber.Content.ReadFromJsonAsync<EmployeeDto>())!.Email.Should().Be($"edin.{local}@ottoman.test");
    }

    [Fact]
    public async Task Registering_an_existing_address_rehearses_the_account_write_and_leaves_nothing_behind()
    {
        var c = api.CreateClient();
        var email = TestClient.UniqueEmail();
        (await c.Register(email)).EnsureSuccessStatusCode();
        for (var i = 0; i < 3; i++) (await c.Register(email)).StatusCode.Should().Be(HttpStatusCode.Accepted);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.Users.CountAsync(u => u.Email!.StartsWith("decoy-"))).Should().Be(0, "the rehearsal is always rolled back");
        (await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant())).Should().Be(1);
        (await api.CreateClient().Login(email, TestClient.StrongPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Ana\u0000Anić")]
    [InlineData("Ana‮ćina")]
    [InlineData("Ana⁦x⁩")]
    [InlineData("Ana\u0007")]
    public async Task Control_and_bidi_characters_are_refused_in_names_and_free_text(string bad)
    {
        var admin = await Admin();
        var brand = await admin.PostAsJsonAsync("/api/v1/brands", new NameRequest(bad));
        brand.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await brand.Content.ReadAsStringAsync()).Should().Contain("Tekst sadrži nedozvoljene znakove.");

        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(bad, "Anić", TestClient.UniqueEmail(), null, TestClient.StrongPassword)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.GetAsync($"/api/v1/products?search={Uri.EscapeDataString(bad)}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Line_breaks_tabs_and_ordinary_unicode_stay_allowed()
    {
        var admin = await Admin();
        (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest($"Šišanje ✂ {Guid.NewGuid():N}"[..30], "Prvi red\nDrugi\tred")))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public void Failed_database_commands_are_logged_as_warnings_not_errors()
    {
        using var scope = api.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<OttomanDbContext>>();
        var warnings = options.FindExtension<CoreOptionsExtension>()!.WarningsConfiguration;
        warnings.GetLevel(RelationalEventId.CommandError).Should().Be(LogLevel.Warning);
        warnings.GetLevel(CoreEventId.SaveChangesFailed).Should().Be(LogLevel.Warning);
    }
}
