using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Accounts;
using Ottoman.Application.Audit;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Application.Push;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

/// <summary>"Izmijeni lozinku" on the edit dialog: the admin sets a new password without knowing the old one.</summary>
[Collection("api")]
public class AdminSetPasswordTests(ApiFixture api)
{
    private const string NewPassword = "Brand_New_Pass1!";

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    [Fact]
    public async Task The_new_password_works_the_old_one_and_open_sessions_do_not_and_phones_are_forgotten()
    {
        var admin = await Admin();
        var (email, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var token = $"fcm_{Guid.NewGuid():N}:APA91b{Guid.NewGuid():N}";
        (await client.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(token, "android"))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var res = await admin.PostAsJsonAsync($"/api/v1/users/{auth.User.Id}/password", new AdminSetPasswordRequest(NewPassword));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await api.CreateClient().Login(email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.CreateClient().Login(email, TestClient.StrongPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "the old session ended");
        await using var scope = api.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<OttomanDbContext>().DeviceTokens.AnyAsync(d => d.Token == token))
            .Should().BeFalse("no phone keeps receiving the account's pushes");
    }

    [Fact]
    public async Task A_barber_account_can_be_reset_too_and_a_lock_ends()
    {
        var admin = await Admin();
        var email = TestClient.UniqueEmail("barber");
        var created = await admin.PostAsJsonAsync("/api/v1/employees",
            new CreateEmployeeRequest("Reset", "Brijač", email, null, "Emp_Pass123!", "Barber", null, null));
        var barber = (await created.Content.ReadFromJsonAsync<EmployeeDto>())!;
        for (var i = 0; i < 5; i++) await api.CreateClient().Login(email, "Wrong_Pass9!");

        (await admin.PostAsJsonAsync($"/api/v1/users/{barber.Id}/password", new AdminSetPasswordRequest(NewPassword))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        (await api.CreateClient().Login(email, NewPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1!")]
    [InlineData("nouppercase1!")]
    [InlineData("NoDigitsHere!")]
    public async Task A_weak_password_is_refused_and_changes_nothing(string weak)
    {
        var admin = await Admin();
        var (email, auth) = await api.CreateClient().NewClient();

        var res = await admin.PostAsJsonAsync($"/api/v1/users/{auth.User.Id}/password", new AdminSetPasswordRequest(weak));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await api.CreateClient().Login(email, TestClient.StrongPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_the_admin_may_do_it_and_only_for_clients_and_barbers()
    {
        var admin = await Admin();
        var (_, target) = await api.CreateClient().NewClient();
        var url = $"/api/v1/users/{target.User.Id}/password";
        var body = new AdminSetPasswordRequest(NewPassword);

        var (_, other) = await api.CreateClient().NewClient();
        (await api.CreateClient().As(other).PostAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.CreateClient().PostAsJsonAsync(url, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.PostAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/password", body)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var adminId = (await admin.GetFromJsonAsync<Ottoman.Application.Auth.UserDto>("/api/v1/me"))!.Id;
        (await admin.PostAsJsonAsync($"/api/v1/users/{adminId}/password", body)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_audit_log_says_who_did_it_to_which_account_and_never_holds_the_password()
    {
        var admin = await Admin();
        var (_, target) = await api.CreateClient().NewClient();

        (await admin.PostAsJsonAsync($"/api/v1/users/{target.User.Id}/password", new AdminSetPasswordRequest(NewPassword))).EnsureSuccessStatusCode();

        var page = await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>(
            $"/api/v1/audit?pageSize=100&search={Uri.EscapeDataString(target.User.Id)}");
        page!.Items.Should().Contain(e => e.Action == "Users.SetPassword" && e.Target == $"id={target.User.Id}");
        System.Text.Json.JsonSerializer.Serialize(page).Should().NotContain(NewPassword);
    }
}
