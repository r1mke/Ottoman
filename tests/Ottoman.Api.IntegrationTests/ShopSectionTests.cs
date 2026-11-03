using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Orders;
using Ottoman.Application.Shop;
using Ottoman.Domain;
using Ottoman.Domain.Entities;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class ShopSectionTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    /// <summary>The blocks of the entry page as a customer sees them, in the admin's order.</summary>
    private static async Task<List<ShopBlockDto>> Blocks(HttpClient c) =>
        (await c.GetFromJsonAsync<ShopOverviewDto>("/api/v1/shop/overview"))!.Blocks.ToList();

    private static string Unique(string p) => $"{p} {Guid.NewGuid().ToString("N")[..6]}";

    private static async Task<ProductDto> NewProduct(HttpClient admin, decimal price = 20m, decimal? sale = null, int stock = 5)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest(Unique("Sekcija proizvod"), "opis", null, null, price, stock, null, true, sale));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    [Fact]
    public async Task A_sale_price_must_be_below_the_regular_price()
    {
        var admin = await Admin();
        var res = await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest(Unique("Akcija"), null, null, null, 10m, 5, null, true, 10m));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await NewProduct(admin, 20m, 15m);
        ok.Price.Should().Be(20m);
        ok.SalePrice.Should().Be(15m);
    }

    [Fact]
    public async Task An_order_is_priced_at_the_sale_price_and_the_client_cannot_choose_it()
    {
        var admin = await Admin();
        var p = await NewProduct(admin, 20m, 12.5m);
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);

        var res = await client.PostAsJsonAsync("/api/v1/orders",
            new CreateOrderRequest([new OrderLineRequest(p.Id, 2)], FulfilmentType.Pickup, PaymentMethod.OnReceipt));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var order = (await res.Content.ReadFromJsonAsync<OrderDto>())!;
        order.TotalPrice.Should().Be(25m);
        order.Items.Single().UnitPrice.Should().Be(12.5m);
    }

    [Fact]
    public async Task The_on_sale_section_lists_discounted_products_and_the_manual_one_keeps_the_admins_order()
    {
        var admin = await Admin();
        var sale = await NewProduct(admin, 40m, 10m);
        var a = await NewProduct(admin);
        var b = await NewProduct(admin);

        var onSale = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Akcije"), ShopSectionKind.OnSale, null, 30))).Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        var manual = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Izbor"), ShopSectionKind.Manual, null, 10, true, [b.Id, a.Id]))).Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;

        var (_, auth) = await api.CreateClient().NewClient();
        var shown = (await Blocks(api.CreateClient().As(auth)));

        shown.Single(s => s.SectionId == onSale.Id).Products!.Select(p => p.Id).Should().Contain(sale.Id)
            .And.OnlyContain(id => id == sale.Id || shown.Single(s => s.SectionId == onSale.Id).Products!.Single(p => p.Id == id).SalePrice != null);
        shown.Single(s => s.SectionId == manual.Id).Products!.Select(p => p.Id).Should().Equal(b.Id, a.Id);
    }

    [Fact]
    public async Task Only_the_admin_manages_sections_and_a_category_section_needs_a_category()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        (await client.PostAsJsonAsync("/api/v1/shop/sections", new ShopSectionRequest("x", ShopSectionKind.NewArrivals, null)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/shop/sections/manage")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await Admin();
        (await admin.PostAsJsonAsync("/api/v1/shop/sections", new ShopSectionRequest(Unique("Bez kat"), ShopSectionKind.Category, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/v1/shop/sections", new ShopSectionRequest(Unique("Prazno"), ShopSectionKind.Manual, null, 10, true, [])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sections_can_be_hidden_reordered_and_deleted()
    {
        var admin = await Admin();
        var first = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Prva"), ShopSectionKind.NewArrivals, null))).Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        var second = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Druga"), ShopSectionKind.NewArrivals, null))).Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;

        var all = (await admin.GetFromJsonAsync<List<ShopSectionAdminDto>>("/api/v1/shop/sections/manage"))!;
        var reversed = all.Select(s => s.Id).Reverse().ToList();
        (await admin.PutAsJsonAsync("/api/v1/shop/sections/order", new ShopSectionOrderRequest(reversed)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<List<ShopSectionAdminDto>>("/api/v1/shop/sections/manage"))!
            .Select(s => s.Id).Should().Equal(reversed);

        // An incomplete order is refused.
        (await admin.PutAsJsonAsync("/api/v1/shop/sections/order", new ShopSectionOrderRequest([first.Id])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.PutAsJsonAsync($"/api/v1/shop/sections/{first.Id}",
            new ShopSectionRequest(first.Title, ShopSectionKind.NewArrivals, null, 10, false))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Blocks(admin)).Should().NotContain(s => s.SectionId == first.Id);

        (await admin.DeleteAsync($"/api/v1/shop/sections/{second.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/v1/shop/sections/{second.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_kind_travels_as_text_because_the_apps_read_it_that_way()
    {
        var admin = await Admin();
        await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Tekst"), ShopSectionKind.OnSale, null));

        var raw = await admin.GetStringAsync("/api/v1/shop/sections/manage");

        raw.Should().Contain("\"kind\":\"OnSale\"");
    }

    [Fact]
    public async Task A_category_section_can_filter_by_brand_alone_or_with_the_category()
    {
        var admin = await Admin();
        var brand = (await (await admin.PostAsJsonAsync("/api/v1/brands", new NameRequest(Unique("Brend")))).Content.ReadFromJsonAsync<BrandDto>())!;
        var other = (await (await admin.PostAsJsonAsync("/api/v1/brands", new NameRequest(Unique("Drugi")))).Content.ReadFromJsonAsync<BrandDto>())!;
        async Task<ProductDto> Make(int brandId) => (await (await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest(Unique("Brend proizvod"), null, brandId, null, 20m, 5, null))).Content.ReadFromJsonAsync<ProductDto>())!;
        var mine = await Make(brand.Id);
        var theirs = await Make(other.Id);

        // Neither a category nor a brand: refused.
        (await admin.PostAsJsonAsync("/api/v1/shop/sections", new ShopSectionRequest(Unique("Prazno"), ShopSectionKind.Category, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // A brand that does not exist: refused.
        (await admin.PostAsJsonAsync("/api/v1/shop/sections", new ShopSectionRequest(Unique("Nema"), ShopSectionKind.Category, null, 10, true, null, 999999)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Samo brend"), ShopSectionKind.Category, null, 10, true, null, brand.Id));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var rule = (await created.Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        rule.BrandId.Should().Be(brand.Id);
        rule.BrandName.Should().Be(brand.Name);

        var shown = (await Blocks(admin));
        var ids = shown.Single(x => x.SectionId == rule.Id).Products!.Select(p => p.Id).ToList();
        ids.Should().Contain(mine.Id).And.NotContain(theirs.Id);
    }

    [Fact]
    public async Task The_overview_counts_what_is_on_the_shelf_per_category_and_brand_and_the_sales()
    {
        var admin = await Admin();
        var cat = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Kat")))).Content.ReadFromJsonAsync<CategoryDto>())!;
        var brand = (await (await admin.PostAsJsonAsync("/api/v1/brands", new NameRequest(Unique("Brend")))).Content.ReadFromJsonAsync<BrandDto>())!;
        async Task<ProductDto> Make(int stock, decimal? sale) => (await (await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest(Unique("Pregled"), null, brand.Id, cat.Id, 40m, stock, null, true, sale))).Content.ReadFromJsonAsync<ProductDto>())!;
        await Make(5, null);
        await Make(5, 30m);     // 25 % off
        await Make(0, null);    // out of stock: not on the shelf

        var (_, auth) = await api.CreateClient().NewClient();
        var overview = (await api.CreateClient().As(auth).GetFromJsonAsync<ShopOverviewDto>("/api/v1/shop/overview"))!;

        overview.Categories.Single(c => c.Id == cat.Id).ProductCount.Should().Be(2);
        overview.Brands.Single(b => b.Id == brand.Id).ProductCount.Should().Be(2);
        overview.Categories.Select(c => c.ProductCount).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task The_product_list_can_be_limited_to_what_is_on_sale()
    {
        var admin = await Admin();
        var regular = await NewProduct(admin, 20m);
        var sale = await NewProduct(admin, 20m, 15m);

        var items = (await admin.GetFromJsonAsync<Ottoman.Application.Common.PagedResult<ProductDto>>("/api/v1/products?onSale=true&pageSize=100"))!.Items;

        items.Should().OnlyContain(p => p.SalePrice != null);
        items.Select(p => p.Id).Should().Contain(sale.Id).And.NotContain(regular.Id);
    }

    [Fact]
    public async Task A_title_must_be_short_enough_for_a_card_and_is_tidied_up()
    {
        var admin = await Admin();

        // Too long, too short, only spaces: refused with a message that says why.
        var tooLong = await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(new string('A', ShopSectionTitle.MaxLength + 1), ShopSectionKind.NewArrivals, null));
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooLong.Content.ReadAsStringAsync()).Should().Contain("najviše");
        (await admin.PostAsJsonAsync("/api/v1/shop/sections", new ShopSectionRequest("A", ShopSectionKind.NewArrivals, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/v1/shop/sections", new ShopSectionRequest("     ", ShopSectionKind.NewArrivals, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Exactly the limit is fine; stray spaces do not count and are collapsed.
        var ok = await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(new string('B', ShopSectionTitle.MaxLength), ShopSectionKind.NewArrivals, null));
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        var tidy = await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest("   Novo    u   ponudi   ", ShopSectionKind.NewArrivals, null));
        tidy.StatusCode.Should().Be(HttpStatusCode.Created);
        (await tidy.Content.ReadFromJsonAsync<ShopSectionAdminDto>())!.Title.Should().Be("Novo u ponudi");
    }

    [Fact]
    public async Task A_section_keeps_its_layout_and_a_banner_carries_a_picture()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var section = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Baner"), ShopSectionKind.Manual, null, 10, true, [product.Id], null, false, ShopSectionLayout.Banner)))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        var grid = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Mreža"), ShopSectionKind.Manual, null, 10, true, [product.Id], null, false, ShopSectionLayout.Grid)))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        var plain = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Red"), ShopSectionKind.Manual, null, 10, true, [product.Id])))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;

        section.Layout.Should().Be(ShopSectionLayout.Banner);
        grid.Layout.Should().Be(ShopSectionLayout.Grid);
        plain.Layout.Should().Be(ShopSectionLayout.Row);
        (await admin.GetStringAsync("/api/v1/shop/sections/manage")).Should().Contain("\"layout\":\"Banner\"");

        var shown = (await Blocks(admin));
        shown.Single(s => s.SectionId == section.Id).Layout.Should().Be(ShopSectionLayout.Banner);
        shown.Single(s => s.SectionId == grid.Id).ImageUrl.Should().BeNull();   // only a banner has a picture of its own
        shown.Single(s => s.SectionId == plain.Id).Layout.Should().Be(ShopSectionLayout.Row);
    }

    [Fact]
    public async Task A_section_has_a_small_amber_text_above_its_title_that_may_be_left_out()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var with = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Naslov"), ShopSectionKind.Manual, null, 10, true, [product.Id], null, true, ShopSectionLayout.Row, "  Nova   kolekcija ")))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        var without = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Bez"), ShopSectionKind.Manual, null, 10, true, [product.Id], null, true, ShopSectionLayout.Row, "   ")))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;

        with.Eyebrow.Should().Be("Nova kolekcija");   // tidied like the title
        without.Eyebrow.Should().BeNull();            // only spaces means none

        var shown = (await Blocks(admin));
        shown.Single(x => x.SectionId == with.Id).Eyebrow.Should().Be("Nova kolekcija");
        var overview = (await admin.GetFromJsonAsync<ShopOverviewDto>("/api/v1/shop/overview"))!;
        overview.Cards.Single(c => c.SectionId == with.Id).Eyebrow.Should().Be("Nova kolekcija");

        (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Dug"), ShopSectionKind.Manual, null, 10, true, [product.Id], null, false, ShopSectionLayout.Row,
                new string('Ž', ShopSectionTitle.EyebrowMaxLength + 1))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_admin_list_says_how_many_products_a_section_shows_right_now()
    {
        var admin = await Admin();
        var product = await NewProduct(admin);
        var full = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Puna"), ShopSectionKind.Manual, null, 10, true, [product.Id])))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;
        // A category that has no such brand: nothing matches, so the section is empty (and hidden from customers).
        var cat = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Prazna"))))
            .Content.ReadFromJsonAsync<CategoryDto>())!;
        var empty = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Prazno"), ShopSectionKind.Category, cat.Id)))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;

        var list = (await admin.GetFromJsonAsync<List<ShopSectionAdminDto>>("/api/v1/shop/sections/manage"))!;

        list.Single(x => x.Id == full.Id).ProductCount.Should().Be(1);
        list.Single(x => x.Id == empty.Id).ProductCount.Should().Be(0);
        (await Blocks(admin)).Should().NotContain(x => x.SectionId == empty.Id);
    }

    /// <summary>Every section, page by page: the other tests leave sections behind, so one page may not hold them all.</summary>
    private async Task<List<ShopSectionAdminDto>> Manage(HttpClient admin)
    {
        var all = new List<ShopSectionAdminDto>();
        for (var page = 1; ; page++)
        {
            var batch = (await admin.GetFromJsonAsync<List<ShopSectionAdminDto>>($"/api/v1/shop/sections/manage?page={page}&pageSize=100"))!;
            all.AddRange(batch);
            if (batch.Count < 100) return all;
        }
    }

    private static ShopSectionRequest Edit(ShopSectionAdminDto s, int? maxItems = null, IReadOnlyList<int>? entries = null, bool? active = null,
        ShopSectionStyle? style = null) =>
        new(s.Title, s.Kind, s.CategoryId, maxItems ?? s.MaxItems, active ?? s.IsActive, s.ProductIds, s.BrandId, s.ShowAsCard, s.Layout,
            s.Eyebrow, entries ?? s.EntryIds, style ?? s.Style);

    [Fact]
    public async Task The_category_brand_and_recommendation_blocks_exist_and_cannot_be_deleted_or_doubled()
    {
        var admin = await Admin();
        var all = await Manage(admin);

        foreach (var kind in new[] { ShopSectionKind.CategoryTiles, ShopSectionKind.BrandTiles, ShopSectionKind.Recommended })
            all.Count(x => x.Kind == kind).Should().Be(1);

        var categories = all.Single(x => x.Kind == ShopSectionKind.CategoryTiles);
        (await admin.DeleteAsync($"/api/v1/shop/sections/{categories.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/v1/shop/sections", new ShopSectionRequest("Kategorije", ShopSectionKind.CategoryTiles, null, 6)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Category_tiles_must_be_an_even_number_so_the_last_row_is_full()
    {
        var admin = await Admin();
        var block = (await Manage(admin)).Single(x => x.Kind == ShopSectionKind.CategoryTiles);

        var odd = await admin.PutAsJsonAsync($"/api/v1/shop/sections/{block.Id}", Edit(block, maxItems: 5));
        odd.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await odd.Content.ReadAsStringAsync()).Should().Contain("paran");
        (await admin.PutAsJsonAsync($"/api/v1/shop/sections/{block.Id}", Edit(block, maxItems: 4))).StatusCode.Should().Be(HttpStatusCode.OK);

        // Hand-picked categories: an odd count is refused too, an even one is kept in the order given.
        var c1 = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Ka")))).Content.ReadFromJsonAsync<CategoryDto>())!;
        var c2 = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Kb")))).Content.ReadFromJsonAsync<CategoryDto>())!;
        var c3 = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Kc")))).Content.ReadFromJsonAsync<CategoryDto>())!;
        (await admin.PutAsJsonAsync($"/api/v1/shop/sections/{block.Id}", Edit(block, entries: [c1.Id, c2.Id, c3.Id])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var saved = await admin.PutAsJsonAsync($"/api/v1/shop/sections/{block.Id}", Edit(block, entries: [c2.Id, c1.Id]));
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saved.Content.ReadFromJsonAsync<ShopSectionAdminDto>())!.EntryIds.Should().Equal(c2.Id, c1.Id);

        // Back to automatic for the other tests.
        (await admin.PutAsJsonAsync($"/api/v1/shop/sections/{block.Id}", Edit(block, maxItems: 6, entries: []))).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_entry_page_lists_its_blocks_in_the_order_the_admin_set_and_leaves_out_categories_without_products()
    {
        var admin = await Admin();
        // A category with a product on the shelf, and one without any.
        var withStock = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Puna")))).Content.ReadFromJsonAsync<CategoryDto>())!;
        var empty = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Prazna")))).Content.ReadFromJsonAsync<CategoryDto>())!;
        await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest(Unique("Blok proizvod"), null, null, withStock.Id, 10m, 5, null));
        var alsoStocked = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Druga puna")))).Content.ReadFromJsonAsync<CategoryDto>())!;
        await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest(Unique("Blok proizvod 2"), null, null, alsoStocked.Id, 10m, 5, null));
        var alsoEmpty = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest(Unique("Druga prazna")))).Content.ReadFromJsonAsync<CategoryDto>())!;
        var all = await Manage(admin);
        var brands = all.Single(x => x.Kind == ShopSectionKind.BrandTiles);
        var categories = all.Single(x => x.Kind == ShopSectionKind.CategoryTiles);
        // Both are picked by hand (the other tests leave many categories behind, so "the top ones" is not stable);
        // only the one that has products may come out.
        (await admin.PutAsJsonAsync($"/api/v1/shop/sections/{categories.Id}", Edit(categories, entries: [withStock.Id, alsoStocked.Id, empty.Id, alsoEmpty.Id])))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            // Brands first, whatever the order was before.
            var order = all.Select(x => x.Id).Where(id => id != brands.Id).Prepend(brands.Id).ToList();
            (await admin.PutAsJsonAsync("/api/v1/shop/sections/order", new ShopSectionOrderRequest(order))).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (_, auth) = await api.CreateClient().NewClient();
            var overview = (await api.CreateClient().As(auth).GetFromJsonAsync<ShopOverviewDto>("/api/v1/shop/overview"))!;

            var tiles = overview.Blocks.Single(b => b.Kind == ShopSectionKind.CategoryTiles).Tiles!;
            tiles.Should().Contain(t => t.Id == withStock.Id);
            tiles.Should().NotContain(t => t.Id == empty.Id || t.Id == alsoEmpty.Id);
            (tiles.Count % 2).Should().Be(0);
            overview.Blocks.Where(b => b.Kind is ShopSectionKind.BrandTiles or ShopSectionKind.CategoryTiles or ShopSectionKind.Recommended)
                .Select(b => b.SectionId).First().Should().Be(brands.Id);
        }
        finally
        {
            await admin.PutAsJsonAsync($"/api/v1/shop/sections/{categories.Id}", Edit(categories, maxItems: 6));
            (await admin.PutAsJsonAsync("/api/v1/shop/sections/order", new ShopSectionOrderRequest(all.Select(x => x.Id).ToList())))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task A_hidden_block_is_not_on_the_entry_page_and_the_look_of_a_section_is_kept()
    {
        var admin = await Admin();
        var recommended = (await Manage(admin)).Single(x => x.Kind == ShopSectionKind.Recommended);
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);

        (await admin.PutAsJsonAsync($"/api/v1/shop/sections/{recommended.Id}", Edit(recommended, active: false))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<ShopOverviewDto>("/api/v1/shop/overview"))!.Blocks.Should().NotContain(b => b.Kind == ShopSectionKind.Recommended);
        (await admin.PutAsJsonAsync($"/api/v1/shop/sections/{recommended.Id}", Edit(recommended, active: true))).StatusCode.Should().Be(HttpStatusCode.OK);

        var product = await NewProduct(admin);
        var look = new ShopSectionStyle(ShopSectionBackground.Amber, ShopTileSize.Large, ShopCardSpan.Full, ShopHeadingAlign.Center, true);
        var section = (await (await admin.PostAsJsonAsync("/api/v1/shop/sections",
            new ShopSectionRequest(Unique("Izgled"), ShopSectionKind.Manual, null, 10, true, [product.Id], null, true, ShopSectionLayout.Row, null, null, look)))
            .Content.ReadFromJsonAsync<ShopSectionAdminDto>())!;

        section.Style.Should().Be(look);
        var overview = (await client.GetFromJsonAsync<ShopOverviewDto>("/api/v1/shop/overview"))!;
        overview.Blocks.Single(b => b.SectionId == section.Id).Style.Should().Be(look);
        overview.Cards.Single(c => c.SectionId == section.Id).Style!.CardSpan.Should().Be(ShopCardSpan.Full);
    }
}
