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

    private OfflineImageStore Store => api.Services.GetRequiredService<OfflineImageStore>();

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private static int _pngSeq;

    /// <summary>A distinct picture per call (the server refuses the same bytes twice for one product); pass the same
    /// <paramref name="unique"/> to get identical bytes.</summary>
    private static byte[] Png(int? unique = null) => TestImages.Png(unique ?? Interlocked.Increment(ref _pngSeq));

    /// <summary>Too big to accept: a PNG signature followed by zeros (the size is checked before anything else).</summary>
    private static byte[] Oversized(int length)
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

    [Fact]
    public async Task The_same_picture_cannot_be_added_twice_to_one_product_but_may_go_on_another()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var other = await NewProduct(admin);
        var url = $"/api/v1/products/{product.Id}/images";

        (await admin.PostAsync(url, Upload(Png(unique: 7001)))).StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadedBefore = Store.Uploaded.Count;

        var again = await admin.PostAsync(url, Upload(Png(unique: 7001), fileName: "renamed.png"));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("već dodana");
        Store.Uploaded.Count.Should().Be(uploadedBefore); // refused before anything reached the store
        (await admin.GetFromJsonAsync<ProductDto>($"/api/v1/products/{product.Id}"))!.Images.Should().ContainSingle();

        (await admin.PostAsync($"/api/v1/products/{other.Id}/images", Upload(Png(unique: 7001)))).StatusCode
            .Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync(url, Upload(Png(unique: 7002)))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Simultaneous_uploads_of_the_same_picture_keep_exactly_one()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var url = $"/api/v1/products/{product.Id}/images";

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => admin.PostAsync(url, Upload(Png(unique: 7100)))));

        results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(5);
        (await admin.GetFromJsonAsync<ProductDto>($"/api/v1/products/{product.Id}"))!.Images.Should().ContainSingle();
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

        var res = await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(Oversized(5 * 1024 * 1024 + 1)));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Store.Uploaded.Count.Should().Be(before);
    }

    [Fact]
    public async Task A_request_far_beyond_the_limit_is_refused_before_anything_is_stored()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);

        var before = Store.Uploaded.Count;
        var res = await admin.PostAsync($"/api/v1/products/{product.Id}/images", Upload(Oversized(7 * 1024 * 1024)));

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
