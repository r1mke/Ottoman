using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Ottoman.Application.Auth;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Whatever goes wrong with a request, the answer is Bosnian and never names a .NET type.</summary>
[Collection("api")]
public class BosnianErrorTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private static readonly string[] Leaks =
        ["Ottoman.", "System.", "could not be converted", "The JSON value", "is required", "LineNumber", "must", "is not valid"];

    private static async Task<string> BadRequestBody(HttpResponseMessage res)
    {
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        foreach (var leak in Leaks) body.Should().NotContain(leak);
        return body;
    }

    private static StringContent Json(string raw) => new(raw, Encoding.UTF8, "application/json");

    [Fact]
    public async Task An_unknown_enum_name_is_a_bosnian_400_without_type_names()
    {
        var body = await BadRequestBody(await (await Admin()).PostAsync("/api/v1/shop/sections",
            Json("{\"title\":\"Sekcija\",\"kind\":\"Nonsense\",\"maxItems\":5}")));
        body.Should().Contain("Vrijednost nije ispravna.").And.Contain("\"kind\"");
    }

    [Fact]
    public async Task Broken_json_and_a_missing_body_are_a_bosnian_400()
    {
        var admin = await Admin();
        (await BadRequestBody(await admin.PostAsync("/api/v1/brands", Json("{\"name\": ")))).Should().Contain("Zahtjev nije ispravan");
        (await BadRequestBody(await admin.PostAsync("/api/v1/brands", Json("")))).Should().Contain("Zahtjev nije ispravan");
        (await BadRequestBody(await admin.PostAsync("/api/v1/brands", Json("{\"name\": 5}")))).Should().Contain("Vrijednost nije ispravna.");
    }

    [Fact]
    public async Task A_bad_query_value_is_a_bosnian_400()
    {
        var body = await BadRequestBody(await (await Admin()).GetAsync("/api/v1/reports/dashboard?from=abc&to=2026-01-01"));
        body.Should().Contain("Vrijednost nije ispravna.");
    }

    [Fact]
    public async Task Default_validator_texts_are_bosnian()
    {
        var admin = await Admin();
        var employee = (await admin.GetFromJsonAsync<List<Ottoman.Application.Employees.EmployeeDto>>("/api/v1/employees"))!.First();
        var res = await admin.PostAsJsonAsync($"/api/v1/employees/{employee.Id}/time-off",
            new Ottoman.Application.Employees.CreateTimeOffRequest(DateTime.UtcNow.AddDays(200), DateTime.UtcNow.AddDays(201), new string('x', 300)));
        (await BadRequestBody(res)).Should().Contain("Razlog");
    }

    [Fact]
    public async Task A_wrong_current_password_is_answered_in_bosnian()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var res = await client.PostAsJsonAsync("/api/v1/me/change-password", new ChangePasswordRequest("Pogresna_Lozinka1!", "Nova_Lozinka123!"));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Pogrešna lozinka.").And.NotContain("Incorrect password");
    }
}
