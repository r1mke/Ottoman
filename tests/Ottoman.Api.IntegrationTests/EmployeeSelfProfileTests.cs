using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Auth;
using Ottoman.Application.Employees;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class EmployeeSelfProfileTests(ApiFixture api)
{
    private const string Password = "Emp_Pass123!";
    private static int _seq;

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(EmployeeDto Barber, HttpClient Http)> NewBarber(HttpClient admin)
    {
        var email = TestClient.UniqueEmail();
        var created = await admin.PostAsJsonAsync("/api/v1/employees",
            new CreateEmployeeRequest("Selma", "Brijač", email, null, Password, "Barber", "Stari opis", null, "Female"));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var barber = (await created.Content.ReadFromJsonAsync<EmployeeDto>())!;
        var http = api.CreateClient().As(await api.CreateClient().LoginOk(email, Password));
        return (barber, http);
    }

    private static MultipartFormDataContent Picture(string? zoom)
    {
        var form = Picture();
        if (zoom is not null) form.Add(new StringContent(zoom), "zoom");
        return form;
    }

    private static MultipartFormDataContent Picture()
    {
        var bytes = TestImages.Png(Interlocked.Increment(ref _seq) + 90000);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "me.png" } };
    }

    [Fact]
    public async Task The_avatar_url_centres_on_the_face_and_carries_the_zoom()
    {
        var admin = await Admin();
        var (barber, http) = await NewBarber(admin);

        var withPhoto = await (await http.PutAsync("/api/v1/me/photo", Picture())).Content.ReadFromJsonAsync<UserDto>();

        withPhoto!.PhotoUrl.Should().Contain("c_thumb,g_face,w_512,h_512,z_1,");
        withPhoto.PhotoZoom.Should().Be(1.0);

        var zoomed = await http.PutAsJsonAsync("/api/v1/me/employee-profile", new UpdateOwnEmployeeProfileRequest(null, "Novi opis", 2.0));
        var me = (await zoomed.Content.ReadFromJsonAsync<UserDto>())!;
        me.PhotoUrl.Should().Contain("z_0.5,");
        me.PhotoZoom.Should().Be(2.0);

        // Everybody who shows this barber gets the cropped picture.
        var listed = (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{barber.Id}"))!;
        listed.PhotoUrl.Should().Contain("z_0.5,");
        listed.PhotoZoom.Should().Be(2.0);
    }

    [Fact]
    public async Task A_barber_edits_their_own_gender_and_bio_but_not_the_title_or_status()
    {
        var admin = await Admin();
        var (barber, http) = await NewBarber(admin);

        var res = await http.PutAsJsonAsync("/api/v1/me/employee-profile", new UpdateOwnEmployeeProfileRequest("Male", "Moj opis", null));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = (await res.Content.ReadFromJsonAsync<UserDto>())!;
        me.Gender.Should().Be("Male");
        me.Bio.Should().Be("Moj opis");
        var seen = (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{barber.Id}"))!;
        seen.Title.Should().Be("Barber");
        seen.IsActive.Should().BeTrue();

        // A blank bio clears it, an absent gender leaves it alone.
        var cleared = (await (await http.PutAsJsonAsync("/api/v1/me/employee-profile",
            new UpdateOwnEmployeeProfileRequest(null, "  ", null))).Content.ReadFromJsonAsync<UserDto>())!;
        cleared.Bio.Should().BeNull();
        cleared.Gender.Should().Be("Male");
    }

    [Fact]
    public async Task The_admin_has_a_picture_gender_and_bio_on_the_account_without_becoming_a_barber()
    {
        var admin = await Admin();

        var withPhoto = await (await admin.PutAsync("/api/v1/me/photo", Picture())).Content.ReadFromJsonAsync<UserDto>();
        withPhoto!.PhotoUrl.Should().NotBeNull();

        var res = await admin.PutAsJsonAsync("/api/v1/me/employee-profile", new UpdateOwnEmployeeProfileRequest("Male", "Vlasnik", null));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = (await res.Content.ReadFromJsonAsync<UserDto>())!;
        me.Gender.Should().Be("Male");
        me.Bio.Should().Be("Vlasnik");
        me.PhotoUrl.Should().NotBeNull();

        var barbers = await admin.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees");
        barbers!.Should().NotContain(e => e.Id == me.Id);

        var cleared = (await (await admin.DeleteAsync("/api/v1/me/photo")).Content.ReadFromJsonAsync<UserDto>())!;
        cleared.PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task Invalid_zoom_gender_or_bio_are_refused()
    {
        var admin = await Admin();
        var (_, http) = await NewBarber(admin);
        const string url = "/api/v1/me/employee-profile";

        (await http.PutAsJsonAsync(url, new UpdateOwnEmployeeProfileRequest(null, null, 0.5))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await http.PutAsJsonAsync(url, new UpdateOwnEmployeeProfileRequest(null, null, 2.5))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await http.PutAsJsonAsync(url, new UpdateOwnEmployeeProfileRequest("Other", null, null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await http.PutAsJsonAsync(url, new UpdateOwnEmployeeProfileRequest(null, new string('x', 501), null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_new_photo_or_removing_it_starts_again_from_the_default_crop()
    {
        var admin = await Admin();
        var (_, http) = await NewBarber(admin);
        await http.PutAsync("/api/v1/me/photo", Picture());
        await http.PutAsJsonAsync("/api/v1/me/employee-profile", new UpdateOwnEmployeeProfileRequest(null, null, 1.8));

        var replaced = (await (await http.PutAsync("/api/v1/me/photo", Picture())).Content.ReadFromJsonAsync<UserDto>())!;
        replaced.PhotoZoom.Should().Be(1.0);

        await http.PutAsJsonAsync("/api/v1/me/employee-profile", new UpdateOwnEmployeeProfileRequest(null, null, 1.5));
        var removed = (await (await http.DeleteAsync("/api/v1/me/photo")).Content.ReadFromJsonAsync<UserDto>())!;
        removed.PhotoUrl.Should().BeNull();
        removed.PhotoZoom.Should().Be(1.0);
    }

    [Fact]
    public async Task A_picture_must_be_a_real_picture()
    {
        var admin = await Admin();
        var (_, http) = await NewBarber(admin);
        var html = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("<html></html>"));
        html.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var res = await http.PutAsync("/api/v1/me/photo", new MultipartFormDataContent { { html, "file", "evil.png" } });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_a_barber_can_use_these_and_only_for_themselves()
    {
        var admin = await Admin();
        var (first, firstHttp) = await NewBarber(admin);
        var (second, _) = await NewBarber(admin);
        await firstHttp.PutAsync("/api/v1/me/photo", Picture());

        // The second barber's picture is untouched by the first one's upload.
        (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{second.Id}"))!.PhotoUrl.Should().BeNull();
        (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{first.Id}"))!.PhotoUrl.Should().NotBeNull();

        // The admin is staff too: their picture and bio are kept on the account (see the admin test above).
        (await admin.PutAsJsonAsync("/api/v1/me/employee-profile", new UpdateOwnEmployeeProfileRequest(null, "x", null)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // A client has no barber profile to edit, and anonymous callers are turned away.
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        (await client.PutAsJsonAsync("/api/v1/me/employee-profile", new UpdateOwnEmployeeProfileRequest(null, "x", null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.CreateClient().PutAsync("/api/v1/me/photo", Picture())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_client_sets_replaces_and_removes_their_own_photo_and_the_admin_sees_it()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);

        var set = await client.PutAsync("/api/v1/me/photo", Picture());
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        var first = (await set.Content.ReadFromJsonAsync<UserDto>())!.PhotoUrl;
        first.Should().NotBeNull();

        var replaced = (await (await client.PutAsync("/api/v1/me/photo", Picture())).Content.ReadFromJsonAsync<UserDto>())!.PhotoUrl;
        replaced.Should().NotBeNull().And.NotBe(first);

        var admin = await Admin();
        var listed = (await admin.GetFromJsonAsync<Ottoman.Application.Common.PagedResult<Ottoman.Application.Clients.ClientListItem>>(
            $"/api/v1/clients?search={Uri.EscapeDataString(auth.User.Email)}"))!.Items.Single();
        listed.PhotoUrl.Should().Be(replaced);

        var removed = (await (await client.DeleteAsync("/api/v1/me/photo")).Content.ReadFromJsonAsync<UserDto>())!;
        removed.PhotoUrl.Should().BeNull();

        // The same checks as for staff pictures: not an image -> refused.
        var html = new ByteArrayContent("<html>"u8.ToArray());
        html.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await client.PutAsync("/api/v1/me/photo", new MultipartFormDataContent { { html, "file", "evil.png" } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_admin_sets_the_zoom_with_the_rest_of_the_barbers_data()
    {
        var admin = await Admin();
        var (barber, _) = await NewBarber(admin);
        await admin.PutAsync($"/api/v1/employees/{barber.Id}/photo", Picture());

        var res = await admin.PutAsJsonAsync($"/api/v1/employees/{barber.Id}",
            new UpdateEmployeeRequest("Selma", "Brijač", null, "Barber", null, true, null, 1.6));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await res.Content.ReadFromJsonAsync<EmployeeDto>())!;
        dto.PhotoZoom.Should().Be(1.6);
        dto.PhotoUrl.Should().Contain("z_0.63,");

        (await admin.PutAsJsonAsync($"/api/v1/employees/{barber.Id}",
            new UpdateEmployeeRequest("Selma", "Brijač", null, "Barber", null, true, null, 3.0))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_client_zooms_their_picture_when_uploading_and_later_and_the_admin_sees_the_same_crop()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);

        var set = await client.PutAsync("/api/v1/me/photo", Picture("1.5"));
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = (await set.Content.ReadFromJsonAsync<UserDto>())!;
        me.PhotoZoom.Should().Be(1.5);
        me.PhotoUrl.Should().Contain("c_thumb,g_face,w_512,h_512,z_0.67,");

        var zoomed = await client.PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(2.0));
        zoomed.StatusCode.Should().Be(HttpStatusCode.OK);
        me = (await zoomed.Content.ReadFromJsonAsync<UserDto>())!;
        me.PhotoZoom.Should().Be(2.0);
        me.PhotoUrl.Should().Contain("z_0.5,");
        (await client.GetFromJsonAsync<UserDto>("/api/v1/me"))!.PhotoZoom.Should().Be(2.0);

        // The admin's list and the client's detail show the same crop.
        var admin = await Admin();
        var listed = (await admin.GetFromJsonAsync<Ottoman.Application.Common.PagedResult<Ottoman.Application.Clients.ClientListItem>>(
            $"/api/v1/clients?search={Uri.EscapeDataString(auth.User.Email)}"))!.Items.Single();
        listed.PhotoUrl.Should().Be(me.PhotoUrl);
        listed.PhotoZoom.Should().Be(2.0);
        var detail = (await admin.GetFromJsonAsync<Ottoman.Application.Clients.ClientDetail>($"/api/v1/clients/{auth.User.Id}"))!;
        detail.PhotoUrl.Should().Be(me.PhotoUrl);
        detail.PhotoZoom.Should().Be(2.0);

        // A new picture without a zoom starts from the default crop; removing it resets the zoom too.
        var replaced = (await (await client.PutAsync("/api/v1/me/photo", Picture())).Content.ReadFromJsonAsync<UserDto>())!;
        replaced.PhotoZoom.Should().Be(1.0);
        replaced.PhotoUrl.Should().Contain("z_1,");
        await client.PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(1.4));
        var removed = (await (await client.DeleteAsync("/api/v1/me/photo")).Content.ReadFromJsonAsync<UserDto>())!;
        removed.PhotoUrl.Should().BeNull();
        removed.PhotoZoom.Should().Be(1.0);

        // Nothing to adjust without a picture.
        (await client.PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(1.5)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_zoom_outside_one_to_two_or_not_a_number_is_refused_and_changes_nothing()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var first = (await (await client.PutAsync("/api/v1/me/photo", Picture("1.2"))).Content.ReadFromJsonAsync<UserDto>())!;

        foreach (var bad in new[] { "0.5", "2.5", "1,5", "abc", "NaN" })
            (await client.PutAsync("/api/v1/me/photo", Picture(bad))).StatusCode.Should().Be(HttpStatusCode.BadRequest, bad);
        (await client.PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(0.9))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(2.1))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A valid zoom does not let a fake picture in: the file is still checked by its first bytes.
        var html = new ByteArrayContent("<html>"u8.ToArray());
        html.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await client.PutAsync("/api/v1/me/photo", new MultipartFormDataContent { { html, "file", "evil.png" }, { new StringContent("1.5"), "zoom" } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var me = (await client.GetFromJsonAsync<UserDto>("/api/v1/me"))!;
        me.PhotoUrl.Should().Be(first.PhotoUrl);
        me.PhotoZoom.Should().Be(1.2);

        (await api.CreateClient().PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(1.5)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_barber_can_use_the_same_zoom_route_for_their_own_picture()
    {
        var admin = await Admin();
        var (barber, http) = await NewBarber(admin);
        (await http.PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(1.5)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var set = (await (await http.PutAsync("/api/v1/me/photo", Picture("1.8"))).Content.ReadFromJsonAsync<UserDto>())!;
        set.PhotoZoom.Should().Be(1.8);
        var me = (await (await http.PutAsJsonAsync("/api/v1/me/photo/zoom", new UpdatePhotoZoomRequest(1.6))).Content.ReadFromJsonAsync<UserDto>())!;
        me.PhotoZoom.Should().Be(1.6);
        (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{barber.Id}"))!.PhotoUrl.Should().Contain("z_0.63,");
    }
}
