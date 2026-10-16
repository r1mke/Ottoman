using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Catalog;
using Ottoman.Application.Employees;
using Ottoman.Application.Shop;
using Ottoman.Infrastructure.Images;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class ImageTests(ApiFixture api)
{
    private static int _seq;

    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private FakeImageStore Store => api.Services.GetRequiredService<FakeImageStore>();

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private static byte[] Png(int length = 64)
    {
        var bytes = new byte[length];
        PngHead.CopyTo(bytes, 0);
        return bytes;
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string contentType = "image/png", string fileName = "a.png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<ProductDto> NewProduct(HttpClient admin)
    {
        var name = $"Slika proizvod {Interlocked.Increment(ref _seq)} {Guid.NewGuid():N}";
        var res = await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest(name, "opis", null, null, 10m, 5, null, true));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    [Fact]
    public async Task An_admin_can_attach_a_picture_and_it_comes_back_as_a_url()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        product.Images.Should().BeEmpty();
        product.ImageUrl.Should().BeNull();

        var res = await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(Png()));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await res.Content.ReadFromJsonAsync<ProductDto>())!;

        updated.Images.Should().ContainSingle();
        updated.ImageUrl.Should().StartWith("https://res.cloudinary.com/testcloud/image/upload/").And.Contain("ottoman/products/");
        Store.Uploaded.Should().Contain(k => updated.ImageUrl!.EndsWith(k));

        // The list and detail endpoints carry it too.
        var listed = await admin.GetFromJsonAsync<PagedResult>($"/api/v1/products?search={Uri.EscapeDataString(product.Name)}");
        listed!.Items.Single().ImageUrl.Should().Be(updated.ImageUrl);
    }

    private record PagedResult(List<ProductDto> Items, int Total);

    [Fact]
    public async Task The_file_type_is_decided_by_the_bytes_not_by_what_the_client_claims()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var before = Store.Uploaded.Count;

        var html = System.Text.Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");
        var res = await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(html, "image/png", "evil.png"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Store.Uploaded.Count.Should().Be(before);
        (await admin.GetFromJsonAsync<ProductDto>($"/api/v1/products/{product.Id}"))!.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task A_picture_over_the_limit_is_refused_before_it_is_stored()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var before = Store.Uploaded.Count;

        var res = await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(Png(5 * 1024 * 1024 + 1)));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Store.Uploaded.Count.Should().Be(before);
    }

    [Fact]
    public async Task A_request_far_beyond_the_limit_is_refused_before_anything_is_stored()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);

        var before = Store.Uploaded.Count;
        var res = await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(Png(7 * 1024 * 1024)));

        res.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        Store.Uploaded.Count.Should().Be(before);
    }

    [Fact]
    public async Task Only_an_admin_can_upload()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var url = $"/api/v1/products/{product.Id}/images";

        (await api.CreateClient().PostAsync(url, Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        (await client.PostAsync(url, Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var staff = api.CreateClient().As(await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword));
        (await staff.PostAsync(url, Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_product_can_hold_only_so_many_pictures()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        for (var i = 0; i < 8; i++)
            (await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Removing_a_picture_removes_it_from_the_product_and_the_store()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var added = (await (await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(Png()))).Content.ReadFromJsonAsync<ProductDto>())!;
        var image = added.Images.Single();

        var res = await admin.DeleteAsync($"/api/v1/products/{product.Id}/images/{image.Id}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<ProductDto>())!.Images.Should().BeEmpty();
        Store.Deleted.Should().Contain(k => image.Url.EndsWith(k));

        // Another product's picture cannot be removed through this product's URL.
        var other = await NewProduct(admin);
        var foreign = (await (await admin.PostAsync($"/api/v1/products/{other.Id}/images", Upload(Png()))).Content.ReadFromJsonAsync<ProductDto>())!;
        (await admin.DeleteAsync($"/api/v1/products/{product.Id}/images/{foreign.Images.Single().Id}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_service_picture_can_be_replaced_and_cleared()
    {
        var admin = await Admin();
        var created = await admin.PostAsJsonAsync("/api/v1/services",
            new ServiceUpsertRequest($"Usluga slika {Guid.NewGuid():N}", "x", 10m, 30));
        var service = (await created.Content.ReadFromJsonAsync<ServiceDto>())!;

        var first = (await (await admin.PutAsync($"/api/v1/services/{service.Id}/image", Upload(Png()))).Content.ReadFromJsonAsync<ServiceDto>())!;
        first.ImageUrl.Should().NotBeNull();

        var second = (await (await admin.PutAsync($"/api/v1/services/{service.Id}/image", Upload(Png()))).Content.ReadFromJsonAsync<ServiceDto>())!;
        second.ImageUrl.Should().NotBe(first.ImageUrl);
        Store.Deleted.Should().Contain(k => first.ImageUrl!.EndsWith(k), "the replaced picture is not left behind");

        var cleared = (await (await admin.DeleteAsync($"/api/v1/services/{service.Id}/image")).Content.ReadFromJsonAsync<ServiceDto>())!;
        cleared.ImageUrl.Should().BeNull();
        Store.Deleted.Should().Contain(k => second.ImageUrl!.EndsWith(k));

        (await admin.PutAsync("/api/v1/services/999999/image", Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_employee_photo_can_be_set()
    {
        var admin = await Admin();
        var employees = (await admin.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!;
        var id = employees.First().Id;

        var res = await admin.PutAsync($"/api/v1/employees/{id}/photo", Upload(Png()));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadFromJsonAsync<EmployeeDto>())!.PhotoUrl.Should().StartWith("https://res.cloudinary.com/testcloud/");
        (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{id}"))!.PhotoUrl.Should().NotBeNull();
    }

    [Fact]
    public async Task News_carry_a_picture_that_reaches_the_recipients_inbox()
    {
        var admin = await Admin();
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var created = await admin.PostAsJsonAsync("/api/v1/announcements", new Ottoman.Application.Notifications.AnnouncementRequest(
            $"Akcija {Guid.NewGuid():N}", "Šišanje -10%", Ottoman.Domain.AnnouncementAudience.User, auth.User.Id));
        var announcement = (await created.Content.ReadFromJsonAsync<Ottoman.Application.Notifications.AnnouncementDto>())!;
        announcement.ImageUrl.Should().BeNull();

        var res = await admin.PutAsync($"/api/v1/announcements/{announcement.Id}/image", Upload(Png()));

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var withImage = (await res.Content.ReadFromJsonAsync<Ottoman.Application.Notifications.AnnouncementDto>())!;
        withImage.ImageUrl.Should().StartWith("https://res.cloudinary.com/testcloud/").And.Contain("ottoman/news/");
        var inbox = (await client.GetFromJsonAsync<Ottoman.Application.Common.PagedResult<Ottoman.Application.Notifications.NotificationDto>>("/api/v1/notifications"))!;
        inbox.Items.Single(n => n.Title == announcement.Title).ImageUrl.Should().Be(withImage.ImageUrl);

        var cleared = await admin.DeleteAsync($"/api/v1/announcements/{announcement.Id}/image");
        (await cleared.Content.ReadFromJsonAsync<Ottoman.Application.Notifications.AnnouncementDto>())!.ImageUrl.Should().BeNull();
        Store.Deleted.Should().Contain(k => withImage.ImageUrl!.EndsWith(k));
    }

    [Fact]
    public async Task Only_an_admin_can_attach_a_news_picture_and_it_must_be_a_real_picture()
    {
        var admin = await Admin();
        var created = await admin.PostAsJsonAsync("/api/v1/announcements", new Ottoman.Application.Notifications.AnnouncementRequest(
            $"Vijest {Guid.NewGuid():N}", "tekst", Ottoman.Domain.AnnouncementAudience.All));
        var id = (await created.Content.ReadFromJsonAsync<Ottoman.Application.Notifications.AnnouncementDto>())!.Id;
        var (_, auth) = await api.CreateClient().NewClient();

        (await api.CreateClient().PutAsync($"/api/v1/announcements/{id}/image", Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await api.CreateClient().As(auth).PutAsync($"/api/v1/announcements/{id}/image", Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var html = System.Text.Encoding.UTF8.GetBytes("<html></html>");
        (await admin.PutAsync($"/api/v1/announcements/{id}/image", Upload(html))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsync("/api/v1/announcements/999999/image", Upload(Png()))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void Cloudinary_signatures_sort_parameters_and_append_the_secret()
    {
        // sha1("folder=a&timestamp=1" + "secret"), independent of the order the parameters are passed in.
        var a = CloudinaryImageStore.Sign("secret", ("timestamp", "1"), ("folder", "a"));
        var b = CloudinaryImageStore.Sign("secret", ("folder", "a"), ("timestamp", "1"));
        var expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData("folder=a&timestamp=1secret"u8));

        a.Should().Be(expected).And.Be(b);
        CloudinaryImageStore.Sign("other", ("folder", "a"), ("timestamp", "1")).Should().NotBe(a);
    }
}
