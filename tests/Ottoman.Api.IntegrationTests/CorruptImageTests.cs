using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ottoman.Application.Catalog;
using Ottoman.Application.Gallery;
using Ottoman.Application.Images;
using Ottoman.Application.Shop;
using Ottoman.Application.Employees;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Images;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Files that start like a picture but are not one end as 400 "Slika nije ispravna." everywhere, never as a 500.</summary>
[Collection("api")]
public class CorruptImageTests(ApiFixture api)
{
    private OfflineImageStore Store => api.Services.GetRequiredService<OfflineImageStore>();

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    /// <summary>A WebP header with nothing after it, a PNG cut to 20 bytes, a JPEG that stops after its first segment.</summary>
    private static readonly byte[][] Corrupt =
    [
        [.. "RIFF"u8, 100, 0, 0, 0, .. "WEBP"u8],
        TestImages.Png(1)[..20],
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, .. "JFIF"u8, 0, 1, 1, 0, 0, 1, 0, 1, 0, 0],
    ];

    private static MultipartFormDataContent Form(byte[] bytes, params (string Name, string Value)[] fields)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", "a.png" } };
        foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
        return form;
    }

    [Fact]
    public async Task Every_upload_endpoint_refuses_a_picture_that_is_only_a_header()
    {
        var admin = await Admin();
        var product = (await (await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest($"Kvar {Guid.NewGuid():N}", null, null, null, 5m, 1, null))).Content.ReadFromJsonAsync<ProductDto>())!;
        var service = (await (await admin.PostAsJsonAsync("/api/v1/services",
            new ServiceUpsertRequest($"Kvar {Guid.NewGuid():N}", "x", 10m, 30))).Content.ReadFromJsonAsync<ServiceDto>())!;
        var brand = (await (await admin.PostAsJsonAsync("/api/v1/brands",
            new NameRequest($"Kvar {Guid.NewGuid():N}"))).Content.ReadFromJsonAsync<BrandDto>())!;
        var style = (await (await admin.PostAsJsonAsync("/api/v1/gallery/categories",
            new HaircutCategoryRequest("Kvar " + Guid.NewGuid().ToString("N")[..8]))).Content.ReadFromJsonAsync<HaircutCategoryDto>())!;
        var section = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest("Kvar " + Guid.NewGuid().ToString("N")[..6], ShopSectionKind.NewArrivals, null)))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        var employee = (await admin.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!.First();
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);

        var calls = new (string Name, Func<byte[], Task<HttpResponseMessage>> Send)[]
        {
            ("product", b => admin.PostAsync($"/api/v1/products/{product.Id}/images", Form(b))),
            ("service", b => admin.PutAsync($"/api/v1/services/{service.Id}/image", Form(b))),
            ("brand logo", b => admin.PutAsync($"/api/v1/brands/{brand.Id}/logo", Form(b))),
            ("employee", b => admin.PutAsync($"/api/v1/employees/{employee.Id}/photo", Form(b))),
            ("gallery", b => admin.PostAsync("/api/v1/gallery", Form(b, ("categoryId", style.Id.ToString()), ("isFeatured", "false")))),
            ("shop card", b => admin.PutAsync($"/api/v1/shop/sections/{section.Id}/image", Form(b))),
            ("salon logo", b => admin.PutAsync("/api/v1/salon/logo", Form(b))),
            ("own photo", b => client.PutAsync("/api/v1/me/photo", Form(b))),
            ("news", b => admin.PostAsync("/api/v1/announcements/with-image",
                Form(b, ("Title", "Vijest"), ("Body", "Tekst"), ("Audience", "Clients"), ("SendEmail", "false")))),
        };

        var before = Store.Uploaded.Count;
        foreach (var (name, send) in calls)
            foreach (var bytes in Corrupt)
            {
                var res = await send(bytes);
                res.StatusCode.Should().Be(HttpStatusCode.BadRequest, $"{name} with {bytes.Length} bytes");
                (await res.Content.ReadAsStringAsync()).Should().Contain(ImageRules.CorruptMessage, name);
            }
        Store.Uploaded.Count.Should().Be(before, "nothing reached the image store");
    }

    private sealed class Answer(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"error\":{\"message\":\"Invalid image file\"}}") });
    }

    private static CloudinaryImageStore Cloudinary(HttpStatusCode status) =>
        new(new HttpClient(new Answer(status)) { BaseAddress = CloudinaryImageStore.BaseAddress },
            Options.Create(new ImageOptions { CloudName = "c", ApiKey = "k", ApiSecret = "s" }), TimeProvider.System);

    [Fact]
    public async Task A_file_cloudinary_cannot_read_is_a_rejected_picture_other_failures_stay_server_errors()
    {
        using var content = new MemoryStream(TestImages.Png(2));
        await FluentActions.Awaiting(() => Cloudinary(HttpStatusCode.BadRequest).UploadAsync(content, "f", default))
            .Should().ThrowAsync<ImageRejectedException>().WithMessage(ImageRules.CorruptMessage);
        await FluentActions.Awaiting(() => Cloudinary(HttpStatusCode.Unauthorized).UploadAsync(new MemoryStream([1]), "f", default))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
