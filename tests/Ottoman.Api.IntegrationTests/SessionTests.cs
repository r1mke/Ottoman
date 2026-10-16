using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Ottoman.Application.Auth;
using Ottoman.Infrastructure.Auth;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class SessionTests(ApiFixture api)
{
    private static async Task<HttpStatusCode> Probe(HttpClient http) =>
        (await http.GetAsync("/api/v1/notifications/unread-count")).StatusCode;

    private async Task<(string Email, AuthResponse Session)> NewSession()
    {
        var (email, auth) = await api.CreateClient().NewClient();
        return (email, auth);
    }

    private HttpClient As(AuthResponse s) => api.CreateClient().As(s);

    [Fact]
    public async Task Signing_out_ends_the_access_token_at_once_not_when_it_expires()
    {
        var (_, session) = await NewSession();
        var http = As(session);
        (await Probe(http)).Should().Be(HttpStatusCode.OK);

        var logout = await api.CreateClient().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = session.RefreshToken });

        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Probe(http)).Should().Be(HttpStatusCode.Unauthorized, "a copied access token must not outlive the sign-out");
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signing_out_of_one_device_leaves_the_same_persons_other_device_signed_in()
    {
        var (email, phone) = await NewSession();
        var laptop = await api.CreateClient().LoginOk(email, TestClient.StrongPassword);

        await api.CreateClient().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = phone.RefreshToken });

        (await Probe(As(phone))).Should().Be(HttpStatusCode.Unauthorized);
        (await Probe(As(laptop))).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refreshing_keeps_the_session_alive_and_the_new_token_works()
    {
        var (_, session) = await NewSession();

        var refreshed = await (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }))
            .Content.ReadFromJsonAsync<AuthResponse>();

        refreshed!.AccessToken.Should().NotBe(session.AccessToken);
        (await Probe(As(refreshed))).Should().Be(HttpStatusCode.OK);

        await api.CreateClient().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = refreshed.RefreshToken });
        (await Probe(As(refreshed))).Should().Be(HttpStatusCode.Unauthorized, "signing out with the rotated token ends the same session");
        (await Probe(As(session))).Should().Be(HttpStatusCode.Unauthorized, "the older access token belongs to that session as well");
    }

    [Fact]
    public async Task Changing_the_password_ends_every_session()
    {
        var (email, first) = await NewSession();
        var second = await api.CreateClient().LoginOk(email, TestClient.StrongPassword);

        var change = await As(first).PostAsJsonAsync("/api/v1/me/change-password",
            new { currentPassword = TestClient.StrongPassword, newPassword = "Nova_Lozinka_9!" });

        change.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Probe(As(first))).Should().Be(HttpStatusCode.Unauthorized);
        (await Probe(As(second))).Should().Be(HttpStatusCode.Unauthorized);
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Nova_Lozinka_9!" })).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logging_out_with_an_unknown_or_repeated_token_is_harmless()
    {
        var (_, session) = await NewSession();

        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = "does-not-exist" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = session.RefreshToken })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = session.RefreshToken })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_correctly_signed_token_without_a_session_is_refused()
    {
        var (_, session) = await NewSession();
        var options = api.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var userId = new JsonWebTokenHandler().ReadJsonWebToken(session.AccessToken).Subject;

        string Forge(IEnumerable<Claim> claims) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256),
        });

        var noSession = Forge([new Claim("sub", userId), new Claim("role", "Client")]);
        var strangerSession = Forge([new Claim("sub", userId), new Claim("role", "Client"), new Claim("sid", Guid.NewGuid().ToString("N"))]);

        foreach (var token in new[] { noSession, strangerSession })
        {
            var http = api.CreateClient();
            http.DefaultRequestHeaders.Authorization = new("Bearer", token);
            (await Probe(http)).Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Blocking_ends_the_session_too_and_unblocking_does_not_resurrect_a_signed_out_one()
    {
        var (email, session) = await NewSession();
        var admin = api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));
        var id = new JsonWebTokenHandler().ReadJsonWebToken(session.AccessToken).Subject;

        await admin.PostAsync($"/api/v1/clients/{id}/block", null);
        await admin.PostAsync($"/api/v1/clients/{id}/unblock", null);

        (await Probe(As(session))).Should().Be(HttpStatusCode.Unauthorized,
            "blocking revoked the refresh tokens, so the sign-in is gone for good");
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = TestClient.StrongPassword })).StatusCode
            .Should().Be(HttpStatusCode.OK, "but the person can simply sign in again");
    }

    [Fact]
    public async Task The_access_token_carries_the_session_id_and_it_matches_the_refresh_session()
    {
        var (_, session) = await NewSession();

        var sid = new JsonWebTokenHandler().ReadJsonWebToken(session.AccessToken).GetClaim("sid").Value;

        Guid.TryParseExact(sid, "N", out var parsed).Should().BeTrue();
        parsed.Should().NotBe(Guid.Empty);
        var refreshed = await (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }))
            .Content.ReadFromJsonAsync<AuthResponse>();
        new JsonWebTokenHandler().ReadJsonWebToken(refreshed!.AccessToken).GetClaim("sid").Value.Should().Be(sid);
    }
}
