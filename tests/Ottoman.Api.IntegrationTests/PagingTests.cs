using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Catalog;
using Ottoman.Application.Employees;
using Ottoman.Application.Shop;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class PagingTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private static int Total(HttpResponseMessage r) => int.Parse(r.Headers.GetValues("X-Total-Count").Single());

    [Fact]
    public async Task A_lookup_list_is_paged_and_reports_how_many_there_are_in_total()
    {
        var admin = await Admin();
        var tag = Guid.NewGuid().ToString("N")[..8];
        for (var i = 1; i <= 5; i++)
            (await admin.PostAsJsonAsync("/api/v1/brands", new NameRequest($"Brend {tag} {i}"))).EnsureSuccessStatusCode();

        var first = await admin.GetAsync($"/api/v1/brands?search={tag}&page=1&pageSize=2");
        var second = await admin.GetAsync($"/api/v1/brands?search={tag}&page=2&pageSize=2");
        var last = await admin.GetAsync($"/api/v1/brands?search={tag}&page=3&pageSize=2");
        var beyond = await admin.GetAsync($"/api/v1/brands?search={tag}&page=4&pageSize=2");

        (await first.Content.ReadFromJsonAsync<List<BrandDto>>())!.Select(b => b.Name).Should().Equal($"Brend {tag} 1", $"Brend {tag} 2");
        (await second.Content.ReadFromJsonAsync<List<BrandDto>>())!.Select(b => b.Name).Should().Equal($"Brend {tag} 3", $"Brend {tag} 4");
        (await last.Content.ReadFromJsonAsync<List<BrandDto>>())!.Select(b => b.Name).Should().Equal($"Brend {tag} 5");
        (await beyond.Content.ReadFromJsonAsync<List<BrandDto>>())!.Should().BeEmpty();
        Total(first).Should().Be(5);
        Total(beyond).Should().Be(5, "the total does not depend on the page asked for");
    }

    [Fact]
    public async Task No_list_can_be_asked_for_more_than_the_maximum_page_size()
    {
        var admin = await Admin();
        var tag = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 105; i++)
            (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest($"Kat {tag} {i:000}"))).EnsureSuccessStatusCode();

        var res = await admin.GetAsync($"/api/v1/categories?search={tag}&pageSize=100000");

        (await res.Content.ReadFromJsonAsync<List<CategoryDto>>())!.Should().HaveCount(100, "the cap is 100 whatever is requested");
        Total(res).Should().Be(105);
        var rest = await admin.GetAsync($"/api/v1/categories?search={tag}&page=2&pageSize=100000");
        (await rest.Content.ReadFromJsonAsync<List<CategoryDto>>())!.Should().HaveCount(5);
    }

    [Fact]
    public async Task Without_any_parameters_the_first_page_of_up_to_a_hundred_comes_back()
    {
        var admin = await Admin();

        var services = await admin.GetAsync("/api/v1/services");
        var employees = await admin.GetAsync("/api/v1/employees");
        var categories = await admin.GetAsync("/api/v1/categories");

        (await services.Content.ReadFromJsonAsync<List<ServiceDto>>())!.Count.Should().BeInRange(1, 100);
        (await employees.Content.ReadFromJsonAsync<List<EmployeeDto>>())!.Count.Should().BeInRange(1, 100);
        (await categories.Content.ReadFromJsonAsync<List<CategoryDto>>())!.Count.Should().BeInRange(1, 100);
        Total(services).Should().BeGreaterThan(0);
        Total(employees).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Services_and_barbers_can_be_searched_and_paged()
    {
        var admin = await Admin();

        var found = await admin.GetAsync("/api/v1/services?search=brad");
        var services = (await found.Content.ReadFromJsonAsync<List<ServiceDto>>())!;
        services.Should().NotBeEmpty().And.OnlyContain(s => s.Name.Contains("brad", StringComparison.OrdinalIgnoreCase) || (s.Description ?? "").Contains("brad", StringComparison.OrdinalIgnoreCase));
        Total(found).Should().Be(services.Count);

        var one = await admin.GetAsync("/api/v1/services?pageSize=1");
        (await one.Content.ReadFromJsonAsync<List<ServiceDto>>())!.Should().HaveCount(1);
        Total(one).Should().BeGreaterThan(1);

        var barber = await admin.GetAsync("/api/v1/employees?search=Kerim");
        var barbers = (await barber.Content.ReadFromJsonAsync<List<EmployeeDto>>())!;
        barbers.Should().ContainSingle(e => e.FirstName == "Kerim");
        barbers.Single().ServiceIds.Should().NotBeEmpty("service ids still come with each barber");

        var byTitle = await admin.GetAsync("/api/v1/employees?search=Junior");
        (await byTitle.Content.ReadFromJsonAsync<List<EmployeeDto>>())!.Should().Contain(e => e.FirstName == "Tarik");
    }

    [Fact]
    public async Task A_barber_list_page_keeps_the_privacy_rules_of_the_full_list()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var admin = await Admin();

        var asClient = (await (await client.GetAsync("/api/v1/employees?pageSize=1")).Content.ReadFromJsonAsync<List<EmployeeDto>>())!;
        var asAdmin = (await (await admin.GetAsync("/api/v1/employees?pageSize=1")).Content.ReadFromJsonAsync<List<EmployeeDto>>())!;

        asClient.Single().Email.Should().BeNull("contact details are for admins only");
        asAdmin.Single().Email.Should().NotBeNull();
    }

    [Fact]
    public async Task Time_off_and_review_summaries_are_paged_too()
    {
        var admin = await Admin();
        var kerim = (await (await admin.GetAsync("/api/v1/employees?search=Kerim")).Content.ReadFromJsonAsync<List<EmployeeDto>>())!.Single();
        for (var i = 1; i <= 3; i++)
            (await admin.PostAsJsonAsync($"/api/v1/employees/{kerim.Id}/time-off",
                new CreateTimeOffRequest(DateTime.UtcNow.AddDays(300 + i * 2), DateTime.UtcNow.AddDays(301 + i * 2), $"Odmor {i}"))).EnsureSuccessStatusCode();

        var page = await admin.GetAsync($"/api/v1/employees/{kerim.Id}/time-off?pageSize=2");

        (await page.Content.ReadFromJsonAsync<List<TimeOffDto>>())!.Should().HaveCount(2);
        Total(page).Should().BeGreaterThanOrEqualTo(3);

        var summary = await admin.GetAsync("/api/v1/reviews/summary?targetType=Employee&pageSize=1");
        summary.EnsureSuccessStatusCode();
        summary.Headers.Contains("X-Total-Count").Should().BeTrue();
    }

    [Fact]
    public async Task Pages_start_at_one_and_nonsense_values_are_corrected_not_trusted()
    {
        var admin = await Admin();

        var res = await admin.GetAsync("/api/v1/brands?page=-5&pageSize=0");

        res.EnsureSuccessStatusCode();
        (await res.Content.ReadFromJsonAsync<List<BrandDto>>())!.Count.Should().BeLessThanOrEqualTo(1, "a page size of 0 is raised to 1");
    }
}
