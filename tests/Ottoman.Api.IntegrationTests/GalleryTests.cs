using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Application.Gallery;
using Ottoman.Application.Salon;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class GalleryTests(ApiFixture api)
{
    private static int _seq;

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<HttpClient> Client()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        return api.CreateClient().As(auth);
    }

    private static MultipartFormDataContent Upload(int categoryId, string? employeeId = null, bool featured = false, string? caption = null, int? seed = null)
    {
        var bytes = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BitConverter.GetBytes(seed ?? Interlocked.Increment(ref _seq) + 70000).CopyTo(bytes, 8);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent
        {
            { file, "file", "cut.png" },
            { new StringContent(categoryId.ToString()), "categoryId" },
            { new StringContent(featured ? "true" : "false"), "isFeatured" },
        };
        if (employeeId is not null) form.Add(new StringContent(employeeId), "employeeId");
        if (caption is not null) form.Add(new StringContent(caption), "caption");
        return form;
    }

    private async Task<HaircutCategoryDto> NewCategory(HttpClient admin)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/gallery/categories", new HaircutCategoryRequest("Stil " + Guid.NewGuid().ToString("N")[..8]));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<HaircutCategoryDto>())!;
    }

    private async Task<EmployeeDto> NewBarber(HttpClient admin)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/employees",
            new CreateEmployeeRequest("Selma", "Brijač", TestClient.UniqueEmail(), null, "Emp_Pass123!", "Barber", null, null, "Female"));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<EmployeeDto>())!;
    }

    [Fact]
    public async Task A_new_database_has_the_usual_styles_and_everybody_can_browse_them()
    {
        var client = await Client();

        var categories = (await client.GetFromJsonAsync<List<HaircutCategoryDto>>("/api/v1/gallery/categories"))!;

        categories.Select(c => c.Name).Should().Contain(["Buzz cut", "French crop", "Duga kosa"]);
    }

    [Fact]
    public async Task The_admin_adds_a_work_with_its_style_and_barber_and_clients_can_filter_by_both()
    {
        var admin = await Admin();
        var style = await NewCategory(admin);
        var other = await NewCategory(admin);
        var barber = await NewBarber(admin);

        var added = await admin.PostAsync("/api/v1/gallery", Upload(style.Id, barber.Id, featured: true, caption: "Skin fade"));
        added.StatusCode.Should().Be(HttpStatusCode.Created);
        var item = (await added.Content.ReadFromJsonAsync<GalleryItemDto>())!;
        item.CategoryName.Should().Be(style.Name);
        item.EmployeeId.Should().Be(barber.Id);
        item.EmployeeName.Should().Be("Selma Brijač");
        item.IsFeatured.Should().BeTrue();
        item.ImageUrl.Should().StartWith("http");
        (await admin.PostAsync("/api/v1/gallery", Upload(other.Id, barber.Id))).StatusCode.Should().Be(HttpStatusCode.Created);

        var client = await Client();
        var byStyle = (await client.GetFromJsonAsync<PagedResult<GalleryItemDto>>($"/api/v1/gallery?categoryId={style.Id}"))!;
        byStyle.Items.Should().ContainSingle().Which.Id.Should().Be(item.Id);
        var byBarber = (await client.GetFromJsonAsync<PagedResult<GalleryItemDto>>($"/api/v1/gallery?employeeId={barber.Id}"))!;
        byBarber.Items.Should().HaveCount(2);
        var featured = (await client.GetFromJsonAsync<PagedResult<GalleryItemDto>>($"/api/v1/gallery?featured=true&employeeId={barber.Id}"))!;
        featured.Items.Should().ContainSingle().Which.Id.Should().Be(item.Id);
        (await admin.GetFromJsonAsync<List<HaircutCategoryDto>>("/api/v1/gallery/categories"))!
            .Single(c => c.Id == style.Id).Count.Should().Be(1);
    }

    [Fact]
    public async Task Only_the_admin_changes_the_gallery()
    {
        var admin = await Admin();
        var style = await NewCategory(admin);
        var item = (await (await admin.PostAsync("/api/v1/gallery", Upload(style.Id))).Content.ReadFromJsonAsync<GalleryItemDto>())!;
        var client = await Client();

        (await client.PostAsync("/api/v1/gallery", Upload(style.Id))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync($"/api/v1/gallery/{item.Id}", new UpdateGalleryItemRequest(style.Id, null, null, true)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"/api/v1/gallery/{item.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/v1/gallery/categories", new HaircutCategoryRequest("X"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.CreateClient().GetAsync("/api/v1/gallery")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_work_can_be_edited_and_removed_and_bad_input_is_refused()
    {
        var admin = await Admin();
        var style = await NewCategory(admin);
        var other = await NewCategory(admin);
        var item = (await (await admin.PostAsync("/api/v1/gallery", Upload(style.Id))).Content.ReadFromJsonAsync<GalleryItemDto>())!;

        var moved = await admin.PutAsJsonAsync($"/api/v1/gallery/{item.Id}", new UpdateGalleryItemRequest(other.Id, null, "  Novi opis ", true));
        moved.StatusCode.Should().Be(HttpStatusCode.OK);
        var edited = (await moved.Content.ReadFromJsonAsync<GalleryItemDto>())!;
        edited.CategoryId.Should().Be(other.Id);
        edited.Caption.Should().Be("Novi opis");
        edited.IsFeatured.Should().BeTrue();

        (await admin.PutAsJsonAsync($"/api/v1/gallery/{item.Id}", new UpdateGalleryItemRequest(999999, null, null, false)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/v1/gallery", Upload(999999))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync("/api/v1/gallery", Upload(style.Id, "nema-takvog"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.DeleteAsync($"/api/v1/gallery/{item.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/v1/gallery/{item.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_same_picture_cannot_be_added_twice()
    {
        var admin = await Admin();
        var style = await NewCategory(admin);
        var seed = Interlocked.Increment(ref _seq) + 75000;

        (await admin.PostAsync("/api/v1/gallery", Upload(style.Id, seed: seed))).StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.PostAsync("/api/v1/gallery", Upload(style.Id, seed: seed))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_style_in_use_cannot_be_deleted_and_names_stay_unique()
    {
        var admin = await Admin();
        var style = await NewCategory(admin);
        await admin.PostAsync("/api/v1/gallery", Upload(style.Id));

        (await admin.DeleteAsync($"/api/v1/gallery/categories/{style.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/v1/gallery/categories", new HaircutCategoryRequest(style.Name))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var unused = await NewCategory(admin);
        var renamed = await admin.PutAsJsonAsync($"/api/v1/gallery/categories/{unused.Id}", new HaircutCategoryRequest("Preimenovano " + unused.Name));
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.DeleteAsync($"/api/v1/gallery/categories/{unused.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PostAsJsonAsync("/api/v1/gallery/categories", new HaircutCategoryRequest("  "))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_salon_profile_is_readable_by_everybody_and_edited_by_the_admin()
    {
        var admin = await Admin();
        var client = await Client();

        var profile = (await client.GetFromJsonAsync<SalonProfileDto>("/api/v1/salon"))!;
        profile.Hours.Should().HaveCount(7);
        profile.Hours.Select(h => h.Day).Should().Equal(1, 2, 3, 4, 5, 6, 0); // Monday first

        var hours = profile.Hours.Select(h => h.Day == 0 ? new OpeningHoursDto(0, 10 * 60, 14 * 60) : h).ToList();
        var update = new UpdateSalonProfileRequest(
            "Ottoman", "Opis salona", "Ferhadija 12", "061 123 456", "https://maps.google.com/?q=ottoman", 43.8586, 18.4310, hours);
        var saved = await admin.PutAsJsonAsync("/api/v1/salon", update);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        var back = (await client.GetFromJsonAsync<SalonProfileDto>("/api/v1/salon"))!;
        back.Description.Should().Be("Opis salona");
        back.MapsUrl.Should().Be("https://maps.google.com/?q=ottoman");
        back.Latitude.Should().Be(43.8586);
        back.Longitude.Should().Be(18.4310);
        back.Hours.Single(h => h.Day == 0).OpenMinutes.Should().Be(600);

        (await client.PutAsJsonAsync("/api/v1/salon", update)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Social links are kept, and cleared by sending nothing.
        (await admin.PutAsJsonAsync("/api/v1/salon", update with { InstagramUrl = "https://instagram.com/ottoman", FacebookUrl = "https://facebook.com/ottoman" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var social = (await client.GetFromJsonAsync<SalonProfileDto>("/api/v1/salon"))!;
        social.InstagramUrl.Should().Be("https://instagram.com/ottoman");
        social.FacebookUrl.Should().Be("https://facebook.com/ottoman");
        (await admin.PutAsJsonAsync("/api/v1/salon", update)).StatusCode.Should().Be(HttpStatusCode.OK);
        var cleared = (await client.GetFromJsonAsync<SalonProfileDto>("/api/v1/salon"))!;
        cleared.InstagramUrl.Should().BeNull();
        cleared.FacebookUrl.Should().BeNull();
    }

    [Fact]
    public async Task Bad_salon_profile_input_is_refused()
    {
        var admin = await Admin();
        var good = (await admin.GetFromJsonAsync<SalonProfileDto>("/api/v1/salon"))!.Hours;
        UpdateSalonProfileRequest With(string phone = "061 123 456", string? maps = null, IReadOnlyList<OpeningHoursDto>? hours = null, double? lat = null, double? lon = null) =>
            new("Ottoman", null, null, phone, maps, lat, lon, hours ?? good);

        (await admin.PutAsJsonAsync("/api/v1/salon", With(phone: "12345"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync("/api/v1/salon", With(maps: "javascript:alert(1)"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync("/api/v1/salon", With() with { InstagramUrl = "javascript:alert(1)" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync("/api/v1/salon", With() with { FacebookUrl = "nije link" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync("/api/v1/salon", With(hours: good.Take(6).ToList()))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync("/api/v1/salon", With(lat: 95, lon: 18))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync("/api/v1/salon", With(lat: 43.8))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var backwards = good.Select(h => h.Day == 1 ? new OpeningHoursDto(1, 18 * 60, 9 * 60) : h).ToList();
        (await admin.PutAsJsonAsync("/api/v1/salon", With(hours: backwards))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_admin_sets_and_removes_the_logo()
    {
        var admin = await Admin();
        var bytes = new byte[64];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BitConverter.GetBytes(Interlocked.Increment(ref _seq) + 80000).CopyTo(bytes, 8);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var set = await admin.PutAsync("/api/v1/salon/logo", new MultipartFormDataContent { { file, "file", "logo.png" } });
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        (await set.Content.ReadFromJsonAsync<SalonProfileDto>())!.LogoUrl.Should().NotBeNull();

        var cleared = (await (await admin.DeleteAsync("/api/v1/salon/logo")).Content.ReadFromJsonAsync<SalonProfileDto>())!;
        cleared.LogoUrl.Should().BeNull();
    }
}
