using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ottoman.Application.Auth;

namespace Ottoman.Api.IntegrationTests;

public static class TestClient
{
    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@ottoman.test";

    public const string StrongPassword = "Str0ng_Pass!";

    public static Task<HttpResponseMessage> Register(this HttpClient c, string email, string password = StrongPassword,
        object? extra = null) =>
        c.PostAsJsonAsync("/api/v1/auth/register", extra ?? new
        {
            firstName = "Test",
            lastName = "Korisnik",
            email,
            phone = (string?)null,
            password,
        });

    public static Task<HttpResponseMessage> Login(this HttpClient c, string email, string password) =>
        c.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

    public static async Task<AuthResponse> LoginOk(this HttpClient c, string email, string password)
    {
        var res = await c.Login(email, password);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    /// <summary>Registers a fresh client and logs them in.</summary>
    public static async Task<(string Email, AuthResponse Auth)> NewClient(this HttpClient c)
    {
        var email = UniqueEmail();
        (await c.Register(email)).EnsureSuccessStatusCode();
        return (email, await c.LoginOk(email, StrongPassword));
    }

    public static HttpClient As(this HttpClient c, AuthResponse auth)
    {
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return c;
    }
}
