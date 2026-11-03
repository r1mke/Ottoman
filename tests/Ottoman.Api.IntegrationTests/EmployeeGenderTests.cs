using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Auth;
using Ottoman.Application.Employees;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class EmployeeGenderTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private static CreateEmployeeRequest NewBarber(string email, string? gender) =>
        new("Ana", "Frizerka", email, null, "Emp_Pass123!", "Barber", null, null, gender);

    [Fact]
    public async Task A_barber_is_created_with_a_gender_and_it_comes_back()
    {
        var admin = await Admin();
        var email = TestClient.UniqueEmail();

        var res = await admin.PostAsJsonAsync("/api/v1/employees", NewBarber(email, "Female"));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var barber = (await res.Content.ReadFromJsonAsync<EmployeeDto>())!;
        barber.Gender.Should().Be("Female");
        (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{barber.Id}"))!.Gender.Should().Be("Female");
    }

    [Fact]
    public async Task Only_Male_or_Female_is_accepted()
    {
        var admin = await Admin();

        (await admin.PostAsJsonAsync("/api/v1/employees", NewBarber(TestClient.UniqueEmail(), "Other")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/v1/employees", NewBarber(TestClient.UniqueEmail(), "female")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_update_changes_the_gender_and_one_without_it_leaves_it_alone()
    {
        var admin = await Admin();
        var created = await admin.PostAsJsonAsync("/api/v1/employees", NewBarber(TestClient.UniqueEmail(), "Male"));
        var id = (await created.Content.ReadFromJsonAsync<EmployeeDto>())!.Id;

        var kept = await admin.PutAsJsonAsync($"/api/v1/employees/{id}",
            new UpdateEmployeeRequest("Ana", "Frizerka", null, "Barber", null, true));
        (await kept.Content.ReadFromJsonAsync<EmployeeDto>())!.Gender.Should().Be("Male");

        var changed = await admin.PutAsJsonAsync($"/api/v1/employees/{id}",
            new UpdateEmployeeRequest("Ana", "Frizerka", null, "Barber", null, true, "Female"));
        (await changed.Content.ReadFromJsonAsync<EmployeeDto>())!.Gender.Should().Be("Female");

        (await admin.PutAsJsonAsync($"/api/v1/employees/{id}",
            new UpdateEmployeeRequest("Ana", "Frizerka", null, "Barber", null, true, "x")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_signed_in_barber_carries_gender_for_the_sign_in_card_and_others_do_not()
    {
        var admin = await Admin();
        var email = TestClient.UniqueEmail();
        await admin.PostAsJsonAsync("/api/v1/employees", NewBarber(email, "Female"));

        var barber = await api.CreateClient().LoginOk(email, "Emp_Pass123!");
        barber.User.Gender.Should().Be("Female");
        barber.User.PhotoUrl.Should().BeNull(); // no photo uploaded yet: the app shows the default avatar

        var me = await api.CreateClient().As(barber).GetFromJsonAsync<UserDto>("/api/v1/me");
        me!.Gender.Should().Be("Female");

        // The admin has its own profile since F15 (gender/photo may be set by other tests on the
        // shared fixture), so only a fresh client is guaranteed to have nothing but the default.
        var (_, client) = await api.CreateClient().NewClient();
        client.User.Gender.Should().BeNull();
        client.User.PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task The_demo_barbers_are_men()
    {
        var admin = await Admin();

        var barbers = (await admin.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees?includeInactive=true"))!;

        barbers.Where(b => b.Email!.EndsWith("@ottoman.local")).Should().OnlyContain(b => b.Gender == "Male");
    }

    [Fact]
    public async Task Announcements_no_longer_take_pictures()
    {
        var admin = await Admin();
        var created = await admin.PostAsJsonAsync("/api/v1/announcements", new Ottoman.Application.Notifications.AnnouncementRequest(
            $"Bez slike {Guid.NewGuid():N}", "tekst", Ottoman.Domain.AnnouncementAudience.All));
        var id = (await created.Content.ReadFromJsonAsync<Ottoman.Application.Notifications.AnnouncementDto>())!.Id;

        var res = await admin.PutAsync($"/api/v1/announcements/{id}/image", new MultipartFormDataContent());

        res.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }
}
