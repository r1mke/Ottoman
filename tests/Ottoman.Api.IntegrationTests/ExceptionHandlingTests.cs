using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ottoman.Api.Filters;
using Ottoman.Application.Auth;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Every exception leaves the API as a Bosnian ProblemDetails with the right status and no internals.</summary>
[Collection("api")]
public class ExceptionHandlingTests(ApiFixture api)
{
    private static async Task<HttpClient> HostWithRoutes()
    {
        var host = await new HostBuilder().ConfigureWebHost(web => web
            .UseTestServer()
            .UseEnvironment("Development")
            .ConfigureServices(s =>
            {
                s.AddExceptionHandler<HttpExceptionHandler>();
                s.AddExceptionHandler<ApplicationExceptionHandler>();
                s.AddProblemDetails();
                s.AddRouting();
            })
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.UseRouting();
                app.UseEndpoints(e =>
                {
                    e.MapGet("/not-found", _ => throw new NotFoundException());
                    e.MapGet("/not-found-named", _ => throw new NotFoundException("Narudžba nije pronađena."));
                    e.MapGet("/invalid", _ => throw new InvalidRequestException("Količina mora biti veća od nule."));
                    e.MapGet("/conflict", _ => throw new ConflictException("Termin je zauzet."));
                    e.MapGet("/forbidden", _ => throw new ForbiddenException());
                    e.MapGet("/unauthorized", _ => throw new UnauthorizedException());
                    e.MapGet("/rule", _ => throw new BusinessRuleException("Otkazana narudžba se ne može izmijeniti."));
                    e.MapGet("/boom", _ => throw new InvalidOperationException("Server=db01;Password=hunter2 failed in Ottoman.Infrastructure.Secret"));
                });
            })).StartAsync();
        return host.GetTestClient();
    }

    [Theory]
    [InlineData("/not-found", HttpStatusCode.NotFound, "Nije pronađeno.", "Podatak nije pronađen.")]
    [InlineData("/not-found-named", HttpStatusCode.NotFound, "Nije pronađeno.", "Narudžba nije pronađena.")]
    [InlineData("/invalid", HttpStatusCode.BadRequest, "Podaci nisu ispravni.", "Količina mora biti veća od nule.")]
    [InlineData("/conflict", HttpStatusCode.Conflict, "Sukob podataka.", "Termin je zauzet.")]
    [InlineData("/forbidden", HttpStatusCode.Forbidden, "Zabranjeno.", "Nemate pravo za ovu radnju.")]
    [InlineData("/unauthorized", HttpStatusCode.Unauthorized, "Prijava je obavezna.", "Prijavite se za nastavak.")]
    [InlineData("/rule", HttpStatusCode.UnprocessableEntity, "Radnja nije dozvoljena.", "Otkazana narudžba se ne može izmijeniti.")]
    public async Task Each_exception_type_maps_to_its_status_and_bosnian_text(string path, HttpStatusCode status, string title, string detail)
    {
        var res = await (await HostWithRoutes()).GetAsync(path);

        res.StatusCode.Should().Be(status);
        var problem = (await res.Content.ReadFromJsonAsync<ProblemDetails>())!;
        problem.Title.Should().Be(title);
        problem.Detail.Should().Be(detail);
        problem.Status.Should().Be((int)status);
    }

    [Fact]
    public async Task An_unexpected_exception_is_a_generic_500_without_internals()
    {
        var res = await (await HostWithRoutes()).GetAsync("/boom");

        res.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().Contain("Došlo je do greške.").And.Contain("Pokušajte ponovo kasnije.");
        foreach (var leak in new[] { "hunter2", "db01", "Ottoman.Infrastructure", "InvalidOperationException", "at Ottoman", "stackTrace" })
            body.Should().NotContain(leak);
    }

    [Fact]
    public async Task A_result_failure_uses_the_same_titles_as_the_exceptions()
    {
        var admin = api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

        var res = await admin.GetAsync("/api/v1/orders/999999999");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await res.Content.ReadFromJsonAsync<ProblemDetails>())!.Title.Should().Be(ProblemTexts.NotFoundTitle);
    }

    [Fact]
    public async Task A_barber_reading_someone_elses_time_off_gets_a_bosnian_403()
    {
        var admin = api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));
        var email = TestClient.UniqueEmail();
        var created = await admin.PostAsJsonAsync("/api/v1/employees",
            new CreateEmployeeRequest("Selma", "Brijač", email, null, "Emp_Pass123!", "Barber", "Opis", null, "Female"));
        var other = (await admin.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!.First(e => e.Email != email);
        var barber = api.CreateClient().As(await api.CreateClient().LoginOk(email, "Emp_Pass123!"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var res = await barber.GetAsync($"/api/v1/employees/{other.Id}/time-off");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = (await res.Content.ReadFromJsonAsync<ProblemDetails>())!;
        problem.Title.Should().Be(ProblemTexts.ForbiddenTitle);
        problem.Detail.Should().Be(ProblemTexts.ForbiddenDetail);
    }
}
