using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Shop;
using Ottoman.Domain.Entities;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class ShopCardTests(ApiFixture api)
{
    private static readonly byte[] PngHead = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static int _pngSeq;

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private static string Unique(string p) => $"{p} {Guid.NewGuid().ToString("N")[..6]}";

    private static MultipartFormDataContent Upload()
    {
        var bytes = new byte[64];
        PngHead.CopyTo(bytes, 0);
        BitConverter.GetBytes(Interlocked.Increment(ref _pngSeq)).CopyTo(bytes, 8);
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "a.png" } };
    }

    /// <summary>An in-stock product with one picture.</summary>
    private static async Task<ProductDto> ProductWithPicture(HttpClient admin, int? brandId = null, decimal? sale = null)
    {
        var created = await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest(Unique("Kartica proizvod"), null, brandId, null, 40m, 5, null, true, sale));
        var p = (await created.Content.ReadFromJsonAsync<ProductDto>())!;
        var withPicture = await admin.PostAsync($"/api/v1/products/{p.Id}/images", Upload());
        withPicture.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await withPicture.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private static async Task<ShopSectionAdminDto> NewSection(HttpClient admin, ShopSectionRequest request)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/shop/sections", request);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
    }

    private async Task<ShopOverviewDto> Overview()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        return (await api.CreateClient().As(auth).GetFromJsonAsync<ShopOverviewDto>("/api/v1/shop/overview"))!;
    }

    [Fact]
    public async Task A_section_marked_as_a_card_shows_on_the_entry_page_with_its_products_picture_unless_the_admin_chose_one()
    {
        var admin = await Admin();
        var product = await ProductWithPicture(admin);
        var card = await NewSection(admin, new ShopSectionRequest(Unique("Kartica"), ShopSectionKind.Manual, null, 10, true, [product.Id], null, true));
        var rowOnly = await NewSection(admin, new ShopSectionRequest(Unique("Samo red"), ShopSectionKind.Manual, null, 10, true, [product.Id]));

        var cards = (await Overview()).Cards;

        var mine = cards.Single(c => c.SectionId == card.Id);
        mine.ProductCount.Should().Be(1);
        mine.ImageUrl.Should().Be(product.ImageUrl); // the automatic cover
        cards.Should().NotContain(c => c.SectionId == rowOnly.Id);

        // The card opens the products behind it.
        var items = (await admin.GetFromJsonAsync<List<ProductDto>>($"/api/v1/shop/sections/{card.Id}/products"))!;
        items.Select(p => p.Id).Should().Equal(product.Id);
    }

    [Fact]
    public async Task An_uploaded_card_picture_is_used_and_picking_a_product_picture_replaces_it()
    {
        var admin = await Admin();
        var product = await ProductWithPicture(admin);
        var section = await NewSection(admin, new ShopSectionRequest(Unique("Slika"), ShopSectionKind.Manual, null, 10, true, [product.Id], null, true));

        var uploaded = await admin.PutAsync($"/api/v1/shop/sections/{section.Id}/image", Upload());
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterUpload = (await uploaded.Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        afterUpload.ImageUrl.Should().Contain("ottoman/shop/");
        afterUpload.ImageProductImageId.Should().BeNull();
        (await Overview()).Cards.Single(c => c.SectionId == section.Id).ImageUrl.Should().Be(afterUpload.ImageUrl);

        var other = await ProductWithPicture(admin);
        var picked = await admin.PutAsJsonAsync($"/api/v1/shop/sections/{section.Id}/image/product",
            new ShopSectionPickedImageRequest(other.Images.Single().Id));
        picked.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterPick = (await picked.Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        afterPick.ImageUrl.Should().Be(other.ImageUrl);
        afterPick.ImageProductImageId.Should().Be(other.Images.Single().Id);

        var cleared = await admin.DeleteAsync($"/api/v1/shop/sections/{section.Id}/image");
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterClear = (await cleared.Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        afterClear.ImageUrl.Should().BeNull();
        (await Overview()).Cards.Single(c => c.SectionId == section.Id).ImageUrl.Should().Be(product.ImageUrl);
    }

    [Fact]
    public async Task Picking_a_picture_that_does_not_exist_or_a_section_that_does_not_exist_is_refused()
    {
        var admin = await Admin();
        var product = await ProductWithPicture(admin);
        var section = await NewSection(admin, new ShopSectionRequest(Unique("Greška"), ShopSectionKind.Manual, null, 10, true, [product.Id]));

        (await admin.PutAsJsonAsync($"/api/v1/shop/sections/{section.Id}/image/product", new ShopSectionPickedImageRequest(999999)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsJsonAsync("/api/v1/shop/sections/999999/image/product", new ShopSectionPickedImageRequest(product.Images.Single().Id)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_the_admin_changes_pictures()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);

        (await client.PutAsync("/api/v1/shop/sections/1/image", Upload())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsync("/api/v1/brands/1/logo", Upload())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_brand_logo_beats_the_product_photo_on_the_entry_page_and_removing_it_brings_the_photo_back()
    {
        var admin = await Admin();
        var brand = (await (await admin.PostAsJsonAsync("/api/v1/brands", new NameRequest(Unique("Logo brend")))).Content.ReadFromJsonAsync<BrandDto>())!;
        brand.LogoUrl.Should().BeNull();
        var product = await ProductWithPicture(admin, brand.Id);

        var before = (await Overview()).Brands.Single(b => b.Id == brand.Id);
        before.IsLogo.Should().BeFalse();
        before.ImageUrl.Should().Be(product.ImageUrl);

        var set = await admin.PutAsync($"/api/v1/brands/{brand.Id}/logo", Upload());
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        var withLogo = (await set.Content.ReadFromJsonAsync<BrandDto>())!;
        withLogo.LogoUrl.Should().Contain("ottoman/brands/");

        var tile = (await Overview()).Brands.Single(b => b.Id == brand.Id);
        tile.IsLogo.Should().BeTrue();
        tile.ImageUrl.Should().Be(withLogo.LogoUrl);

        (await admin.GetFromJsonAsync<List<BrandDto>>("/api/v1/brands"))!.Single(b => b.Id == brand.Id).LogoUrl.Should().Be(withLogo.LogoUrl);

        (await admin.DeleteAsync($"/api/v1/brands/{brand.Id}/logo")).StatusCode.Should().Be(HttpStatusCode.OK);
        var back = (await Overview()).Brands.Single(b => b.Id == brand.Id);
        back.IsLogo.Should().BeFalse();
        back.ImageUrl.Should().Be(product.ImageUrl);
    }
}
