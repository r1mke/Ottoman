using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Auth;
using Ottoman.Application.Clients;
using Ottoman.Application.Common;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class AuthTests(ApiFixture api)
{
    private HttpClient Anon() => api.CreateClient();

    [Fact]
    public async Task Register_then_login_returns_tokens_and_client_role()
    {
        var c = Anon();
        var email = TestClient.UniqueEmail();

        (await c.Register(email)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var auth = await c.LoginOk(email, TestClient.StrongPassword);

        auth.AccessToken.Should().NotBeNullOrEmpty();
        auth.RefreshToken.Should().NotBeNullOrEmpty();
        auth.User.Roles.Should().BeEquivalentTo([Roles.Client]);

        var me = await api.CreateClient().As(auth).GetFromJsonAsync<UserDto>("/api/v1/me");
        me!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Registration_cannot_grant_roles_even_if_the_request_asks_for_them()
    {
        var c = Anon();
        var email = TestClient.UniqueEmail();

        var res = await c.Register(email, extra: new
        {
            firstName = "Hak",
            lastName = "Er",
            email,
            password = TestClient.StrongPassword,
            roles = new[] { "Admin" },
            role = "Admin",
        });
        res.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var auth = await c.LoginOk(email, TestClient.StrongPassword);
        auth.User.Roles.Should().BeEquivalentTo([Roles.Client]);
    }

    [Fact]
    public async Task Registering_an_existing_email_looks_identical_and_creates_no_second_account()
    {
        var c = Anon();
        var email = TestClient.UniqueEmail();

        var first = await c.Register(email);
        var second = await c.Register(email);

        second.StatusCode.Should().Be(first.StatusCode).And.Be(HttpStatusCode.Accepted);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.Users.CountAsync(u => u.Email == email)).Should().Be(1);
        (await db.OutboxMessages.AnyAsync(m => m.Type == nameof(RegistrationAttemptedForExistingAccount)
                                               && m.PayloadJson.Contains(email))).Should().BeTrue();
    }

    [Fact]
    public async Task Registration_enqueues_a_welcome_event_in_the_outbox()
    {
        var email = TestClient.UniqueEmail();
        (await Anon().Register(email)).EnsureSuccessStatusCode();

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.OutboxMessages.AnyAsync(m => m.Type == nameof(UserRegistered) && m.PayloadJson.Contains(email)))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("short1!")]
    [InlineData("nouppercase1!")]
    [InlineData("NOLOWERCASE1!")]
    [InlineData("NoDigitsHere!")]
    [InlineData("NoSymbol123")]
    public async Task Weak_passwords_are_rejected_with_field_errors(string password)
    {
        var res = await Anon().Register(TestClient.UniqueEmail(), password);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await res.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();
        problem!.Errors.Should().ContainKey("Password");
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_are_indistinguishable()
    {
        var c = Anon();
        var (email, _) = await c.NewClient();

        var wrongPassword = await c.Login(email, "Wrong_Pass1!");
        var unknownEmail = await c.Login(TestClient.UniqueEmail("ghost"), "Wrong_Pass1!");

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownEmail.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await wrongPassword.Content.ReadAsStringAsync()).Should().Be(await unknownEmail.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Account_locks_after_five_failed_attempts_even_with_the_right_password()
    {
        var c = Anon();
        var (email, _) = await c.NewClient();

        for (var i = 0; i < 5; i++)
            (await c.Login(email, "Wrong_Pass1!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await c.Login(email, TestClient.StrongPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reuse_of_the_old_one_kills_the_session()
    {
        var c = Anon();
        var (_, first) = await c.NewClient();

        var rotated = await c.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = (await rotated.Content.ReadFromJsonAsync<AuthResponse>())!;
        second.RefreshToken.Should().NotBe(first.RefreshToken);

        // Replaying the already-used token = theft signal.
        var replay = await c.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(first.RefreshToken));
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // ...and the legitimately issued successor is revoked too.
        var successor = await c.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(second.RefreshToken));
        successor.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Concurrent_refreshes_of_one_token_yield_exactly_one_success()
    {
        var c = Anon();
        var (_, auth) = await c.NewClient();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            api.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken))));

        results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(7);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var c = Anon();
        var (_, auth) = await c.NewClient();

        (await c.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(auth.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await c.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoints_require_a_valid_token()
    {
        var c = Anon();
        (await c.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        c.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.jwt");
        (await c.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Client_cannot_reach_admin_endpoints_but_admin_can()
    {
        var (_, client) = await Anon().NewClient();
        (await api.CreateClient().As(client).GetAsync("/api/v1/clients")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await Anon().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
        var res = await api.CreateClient().As(admin).GetAsync("/api/v1/clients?pageSize=5");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await res.Content.ReadFromJsonAsync<PagedResult<ClientListItem>>();
        page!.Items.Should().OnlyContain(i => !i.Email.StartsWith("admin@") && !i.Email.StartsWith("kerim@"));
    }

    [Fact]
    public async Task Employee_is_not_an_admin()
    {
        var employee = await Anon().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword);
        employee.User.Roles.Should().BeEquivalentTo([Roles.Employee]);
        (await api.CreateClient().As(employee).GetAsync("/api/v1/clients")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Profile_update_only_touches_the_callers_own_account()
    {
        var (_, a) = await Anon().NewClient();
        var (_, b) = await Anon().NewClient();

        var res = await api.CreateClient().As(a).PutAsJsonAsync("/api/v1/me",
            new UpdateProfileRequest("Nova", "Imena", "061 123 456"));
        res.EnsureSuccessStatusCode();

        (await api.CreateClient().As(a).GetFromJsonAsync<UserDto>("/api/v1/me"))!.FirstName.Should().Be("Nova");
        (await api.CreateClient().As(b).GetFromJsonAsync<UserDto>("/api/v1/me"))!.FirstName.Should().Be("Test");
    }

    [Fact]
    public async Task Forgot_password_responds_identically_for_known_and_unknown_emails()
    {
        var c = Anon();
        var (email, _) = await c.NewClient();

        var known = await c.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(email));
        var unknown = await c.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(TestClient.UniqueEmail("nobody")));

        known.StatusCode.Should().Be(HttpStatusCode.NoContent);
        unknown.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Password_reset_token_works_once_and_ends_existing_sessions()
    {
        var c = Anon();
        var (email, auth) = await c.NewClient();
        await c.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(email));

        string token;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var msg = await db.OutboxMessages.Where(m => m.Type == nameof(PasswordResetRequested) && m.PayloadJson.Contains(email))
                .OrderByDescending(m => m.Id).FirstAsync();
            var sealedToken = System.Text.Json.JsonSerializer.Deserialize<PasswordResetRequested>(msg.PayloadJson)!.SealedToken;
            token = scope.ServiceProvider.GetRequiredService<Ottoman.Application.Notifications.IResetTokenVault>().Open(sealedToken)!;
        }

        const string newPassword = "Brand_New_Pass1!";
        var ok = await c.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(email, token, newPassword));
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await c.Login(email, TestClient.StrongPassword)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await c.Login(email, newPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await c.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Single use.
        var again = await c.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest(email, token, "Another_Pass1!"));
        again.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_password_revokes_refresh_tokens()
    {
        var c = Anon();
        var (email, auth) = await c.NewClient();

        var res = await api.CreateClient().As(auth).PostAsJsonAsync("/api/v1/me/change-password",
            new ChangePasswordRequest(TestClient.StrongPassword, "Changed_Pass1!"));
        res.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await c.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await c.Login(email, "Changed_Pass1!")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_is_rate_limited_per_ip()
    {
        using var limited = api.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Enabled", "true");
            b.UseSetting("RateLimiting:LoginPerMinute", "3");
        });
        var c = limited.CreateClient();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            codes.Add((await c.Login(TestClient.UniqueEmail("rl"), "Wrong_Pass1!")).StatusCode);

        codes.Take(3).Should().OnlyContain(s => s == HttpStatusCode.Unauthorized);
        codes[3].Should().Be(HttpStatusCode.TooManyRequests);
    }
}
