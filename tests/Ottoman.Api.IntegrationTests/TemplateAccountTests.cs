using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Auth;

namespace Ottoman.Api.IntegrationTests;

/// <summary>The credentials template of the faculty rules: user names desktop / mobile / uposlenik, password "test".</summary>
[Collection("api")]
public class TemplateAccountTests(ApiFixture api)
{
    [Theory]
    [InlineData("desktop", "Admin")]
    [InlineData("mobile", "Client")]
    [InlineData("uposlenik", "Employee")]
    [InlineData("DESKTOP", "Admin")]
    public async Task The_template_accounts_log_in_with_a_user_name_and_test(string userName, string role)
    {
        var auth = await api.CreateClient().LoginOk(userName, "test");
        auth.User.Roles.Should().Contain(role);
    }

    [Fact]
    public async Task Email_login_still_works_and_the_strong_policy_still_applies_elsewhere()
    {
        (await api.CreateClient().LoginOk("adin@ottoman.local", "test")).User.Roles.Should().Contain("Employee");
        (await api.CreateClient().Login("desktop", "wrong")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await api.CreateClient().Login("nepostoji", "test")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Registering or changing a password with "test" is refused as before.
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Ana", "Anić", TestClient.UniqueEmail(), null, "test"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var mobile = api.CreateClient().As(await api.CreateClient().LoginOk("mobile", "test"));
        (await mobile.PostAsJsonAsync("/api/v1/me/change-password", new ChangePasswordRequest("test", "test")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
