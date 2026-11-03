using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Employees;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class EmployeeTitleTests(ApiFixture api)
{
    private const string Url = "/api/v1/employee-titles";

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..Math.Min(40, prefix.Length + 9)];

    private static Task<HttpResponseMessage> Add(HttpClient http, string name) =>
        http.PostAsJsonAsync(Url, new AddEmployeeTitleRequest(name));

    private static async Task<List<EmployeeTitleDto>> List(HttpClient http) =>
        (await http.GetFromJsonAsync<List<EmployeeTitleDto>>(Url))!;

    [Fact]
    public async Task The_list_starts_with_the_usual_titles_and_those_already_in_use()
    {
        var admin = await Admin();

        var titles = await List(admin);

        titles.Select(t => t.Name).Should().Contain(["Master Barber", "Junior Barber", "Barber"]);
        titles.Single(t => t.Name == "Master Barber").InUse.Should().BeGreaterThan(0); // the demo barber Kerim
        titles.Select(t => t.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task An_admin_adds_a_title_and_a_repeat_in_any_case_is_a_conflict()
    {
        var admin = await Admin();
        var name = Unique("Stilist");

        var created = await Add(admin, $"  {name}  ");
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<EmployeeTitleDto>())!.Name.Should().Be(name); // trimmed

        (await Add(admin, name.ToUpperInvariant())).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await List(admin)).Count(t => t.Name == name).Should().Be(1);
    }

    [Fact]
    public async Task A_blank_or_overlong_title_is_rejected()
    {
        var admin = await Admin();

        (await Add(admin, "   ")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Add(admin, new string('x', 101))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Simultaneous_adds_of_the_same_title_keep_exactly_one()
    {
        var admin = await Admin();
        var name = Unique("Paralelna");

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Add(admin, name)));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(5);
        (await List(admin)).Count(t => t.Name == name).Should().Be(1);
    }

    [Fact]
    public async Task An_unused_title_can_be_deleted_but_one_a_barber_has_cannot()
    {
        var admin = await Admin();
        var spare = (await (await Add(admin, Unique("Višak"))).Content.ReadFromJsonAsync<EmployeeTitleDto>())!;
        (await admin.DeleteAsync($"{Url}/{spare.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await List(admin)).Should().NotContain(t => t.Id == spare.Id);
        (await admin.DeleteAsync($"{Url}/{spare.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var master = (await List(admin)).Single(t => t.Name == "Master Barber");
        var refused = await admin.DeleteAsync($"{Url}/{master.Id}");
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Master Barber");
        (await List(admin)).Should().Contain(t => t.Id == master.Id);
    }

    [Fact]
    public async Task Saving_a_barber_with_a_new_title_puts_it_on_the_list()
    {
        var admin = await Admin();
        var title = Unique("Brijač");

        var created = await admin.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "Nov", "Frizer", TestClient.UniqueEmail(), null, "Emp_Pass123!", title, null, null));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var barber = (await created.Content.ReadFromJsonAsync<EmployeeDto>())!;

        var entry = (await List(admin)).Single(t => t.Name == title);
        entry.InUse.Should().Be(1);

        // Changing the barber's title frees the old one for deletion.
        var other = Unique("Majstor");
        (await admin.PutAsJsonAsync($"/api/v1/employees/{barber.Id}",
            new UpdateEmployeeRequest("Nov", "Frizer", null, other, null, true))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"{Url}/{entry.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await List(admin)).Select(t => t.Name).Should().Contain(other);
    }

    [Fact]
    public async Task Only_admins_can_see_or_change_the_list()
    {
        var anonymous = api.CreateClient();
        (await anonymous.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        (await client.GetAsync(Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Add(client, "Hakeri")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"{Url}/1")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var kerim = api.CreateClient().As(await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword));
        (await Add(kerim, "Hakeri")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
