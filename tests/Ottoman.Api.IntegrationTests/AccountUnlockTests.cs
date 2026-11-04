using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Accounts;
using Ottoman.Application.Audit;
using Ottoman.Application.Clients;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class AccountUnlockTests(ApiFixture api)
{
    private const string BarberPassword = "Emp_Pass123!";

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    /// <summary>Five wrong passwords lock the account (Identity: 5 attempts, 15 minutes).</summary>
    private async Task LockOut(string email)
    {
        for (var i = 0; i < 5; i++)
            (await api.CreateClient().Login(email, "Wrong_Pass9!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<ClientListItem> Listed(HttpClient admin, string email) =>
        (await admin.GetFromJsonAsync<PagedResult<ClientListItem>>($"/api/v1/clients?search={Uri.EscapeDataString(email)}"))!.Items.Single();

    private async Task<EmployeeDto> NewBarber(HttpClient admin, string email)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/employees",
            new CreateEmployeeRequest("Zaključani", "Brijač", email, null, BarberPassword, "Barber", null, null));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<EmployeeDto>())!;
    }

    [Fact]
    public async Task A_locked_client_is_shown_to_the_admin_and_can_sign_in_again_after_an_unlock()
    {
        var (email, auth) = await api.CreateClient().NewClient();
        var admin = await Admin();
        (await Listed(admin, email)).LockedUntilUtc.Should().BeNull();

        await LockOut(email);

        // The right password does not help while locked, and the answer is the same as for an unknown account.
        var locked = await api.CreateClient().Login(email, TestClient.StrongPassword);
        var unknown = await api.CreateClient().Login(TestClient.UniqueEmail(), TestClient.StrongPassword);
        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await locked.Content.ReadAsStringAsync()).Should().Be(await unknown.Content.ReadAsStringAsync());

        var listed = await Listed(admin, email);
        listed.LockedUntilUtc.Should().NotBeNull();
        listed.LockedUntilUtc!.Value.Should().BeAfter(DateTime.UtcNow.AddMinutes(10)).And.BeBefore(DateTime.UtcNow.AddMinutes(16));
        (await admin.GetFromJsonAsync<ClientDetail>($"/api/v1/clients/{auth.User.Id}"))!.LockedUntilUtc.Should().NotBeNull();

        var unlocked = await admin.PostAsync($"/api/v1/users/{auth.User.Id}/unlock", null);
        unlocked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unlocked.Content.ReadFromJsonAsync<AccountLockDto>())!.LockedUntilUtc.Should().BeNull();

        (await Listed(admin, email)).LockedUntilUtc.Should().BeNull();
        (await api.CreateClient().Login(email, TestClient.StrongPassword)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The wrong-password count starts again: four more misses do not lock the account.
        for (var i = 0; i < 4; i++) await api.CreateClient().Login(email, "Wrong_Pass9!");
        (await api.CreateClient().Login(email, TestClient.StrongPassword)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Written to the audit log with who and which account.
        var lines = (await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>(
            $"/api/v1/audit?pageSize=100&search={Uri.EscapeDataString(auth.User.Id)}"))!.Items;
        lines.Should().Contain(e => e.Action == "Users.Unlock" && e.Method == "POST" && e.Role == "Admin"
                                    && e.Target == $"id={auth.User.Id}" && e.StatusCode == 200);
    }

    [Fact]
    public async Task A_locked_barber_is_shown_only_to_the_admin_and_can_be_unlocked()
    {
        var admin = await Admin();
        var email = TestClient.UniqueEmail("barber");
        var barber = await NewBarber(admin, email);

        await LockOut(email);
        (await api.CreateClient().Login(email, BarberPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{barber.Id}"))!.LockedUntilUtc.Should().NotBeNull();
        (await admin.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees?includeInactive=true"))!
            .Single(e => e.Id == barber.Id).LockedUntilUtc.Should().NotBeNull();
        // Nobody else (here: a client) learns that a barber is locked.
        var (_, clientAuth) = await api.CreateClient().NewClient();
        (await api.CreateClient().As(clientAuth).GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!
            .Single(e => e.Id == barber.Id).LockedUntilUtc.Should().BeNull();

        (await admin.PostAsync($"/api/v1/users/{barber.Id}/unlock", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{barber.Id}"))!.LockedUntilUtc.Should().BeNull();
        (await api.CreateClient().Login(email, BarberPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_the_admin_can_unlock_and_only_known_client_or_barber_accounts()
    {
        var admin = await Admin();
        var (_, target) = await api.CreateClient().NewClient();
        var url = $"/api/v1/users/{target.User.Id}/unlock";

        var (_, other) = await api.CreateClient().NewClient();
        (await api.CreateClient().As(other).PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var email = TestClient.UniqueEmail("barber");
        await NewBarber(admin, email);
        var barber = api.CreateClient().As(await api.CreateClient().LoginOk(email, BarberPassword));
        (await barber.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await api.CreateClient().PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await admin.PostAsync($"/api/v1/users/{Guid.NewGuid()}/unlock", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The admin's own account is not handled here.
        var adminId = (await admin.GetFromJsonAsync<Ottoman.Application.Auth.UserDto>("/api/v1/me"))!.Id;
        (await admin.PostAsync($"/api/v1/users/{adminId}/unlock", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Unlocking an account that is not locked is harmless.
        (await admin.PostAsync(url, null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
