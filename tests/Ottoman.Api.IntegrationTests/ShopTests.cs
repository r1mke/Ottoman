using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Api.Controllers;
using Ottoman.Application.Common;
using Ottoman.Application.Orders;
using Ottoman.Application.Payments;
using Ottoman.Application.Shop;
using Ottoman.Domain;
using Ottoman.Infrastructure.Orders;
using Ottoman.Infrastructure.Payments;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class ShopTests(ApiFixture api)
{
    private static int _seq;

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, HttpClient Http)> NewClient()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<HttpClient> Employee()
    {
        var auth = await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword);
        return api.CreateClient().As(auth);
    }

    /// <summary>A product only this test uses, so parallel/earlier tests cannot disturb its stock.</summary>
    private async Task<ProductDto> NewProduct(decimal price = 10m, int stock = 10, int? threshold = null, bool active = true)
    {
        var admin = await Admin();
        var name = $"Test proizvod {Interlocked.Increment(ref _seq)} {Guid.NewGuid():N}";
        var res = await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest(name, "opis", null, null, price, stock, threshold, active));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private static Task<HttpResponseMessage> Order(HttpClient http, ProductDto p, int qty = 1,
        PaymentMethod method = PaymentMethod.Card, FulfilmentType f = FulfilmentType.Pickup, string? address = null) =>
        http.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest([new OrderLineRequest(p.Id, qty)], f, method, address));

    private static async Task<OrderDto> Dto(HttpResponseMessage res) => (await res.Content.ReadFromJsonAsync<OrderDto>())!;

    private async Task<int> StockOf(ProductDto p) =>
        (await (await Admin()).GetFromJsonAsync<ProductDto>($"/api/v1/products/{p.Id}"))!.Stock;

    private async Task<string> RefOf(int orderId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        return await db.Orders.Where(o => o.Id == orderId).Join(db.Payments, o => o.PaymentId, p => p.Id, (_, p) => p.ProviderRef).SingleAsync();
    }

    private static string Event(string type, string intent, long amount, string currency = "eur") =>
        "{\"id\":\"evt_" + Guid.NewGuid().ToString("N") + "\",\"type\":\"" + type + "\",\"data\":{\"object\":{\"id\":\"" + intent +
        "\",\"amount\":" + amount + ",\"amount_received\":" + amount + ",\"currency\":\"" + currency + "\"}}}";

    private async Task<HttpResponseMessage> Webhook(string body, string? header = null)
    {
        var http = api.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhook")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        header ??= StripeWebhook.Header(body, ApiFixture.WebhookSecret, DateTimeOffset.UtcNow);
        req.Headers.Add("Stripe-Signature", header);
        return await http.SendAsync(req);
    }

    [Fact]
    public async Task Enums_travel_as_names_not_numbers_so_the_apps_can_read_them()
    {
        var p = await NewProduct();
        var (_, client) = await NewClient();
        var res = await client.PostAsJsonAsync("/api/v1/orders", new { items = new[] { new { productId = p.Id, quantity = 1 } }, fulfilment = "Pickup", paymentMethod = "OnReceipt" });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().Contain("\"status\":\"Placed\"").And.Contain("\"paymentMethod\":\"OnReceipt\"").And.Contain("\"fulfilment\":\"Pickup\"");
    }

    // ------------------------------------------------------------------ catalogue

    [Fact]
    public async Task Catalogue_requires_login_and_only_admins_write()
    {
        (await api.CreateClient().GetAsync("/api/v1/products")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, client) = await NewClient();
        (await client.GetAsync("/api/v1/products")).StatusCode.Should().Be(HttpStatusCode.OK);
        var body = new ProductUpsertRequest("Hak", null, null, null, 5m, 1, null);
        (await client.PostAsJsonAsync("/api/v1/products", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await (await Employee()).PostAsJsonAsync("/api/v1/products", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/v1/brands", new NameRequest("X"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsync("/api/v1/products/1/stock", JsonContent.Create(new StockAdjustRequest(5)))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Search_filter_sort_and_paging_work_on_the_server()
    {
        var (_, client) = await NewClient();
        var all = (await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?pageSize=100"))!;
        all.Items.Should().Contain(p => p.Name.Contains("Dandy"));

        var dandy = (await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?search=dandy"))!;
        dandy.Items.Should().OnlyContain(p => p.BrandName == "Dandy" || p.Name.Contains("Dandy", StringComparison.OrdinalIgnoreCase));

        var byPrice = (await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?sort=PriceDesc&pageSize=100"))!;
        byPrice.Items.Select(p => p.Price).Should().BeInDescendingOrder();

        var page = (await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?pageSize=2&page=1"))!;
        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task Inactive_products_are_hidden_from_clients_but_visible_to_admins()
    {
        var hidden = await NewProduct(active: false);
        var (_, client) = await NewClient();
        var admin = await Admin();

        (await client.GetAsync($"/api/v1/products/{hidden.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetFromJsonAsync<PagedResult<ProductDto>>($"/api/v1/products?search={hidden.Name}&includeInactive=true"))!
            .Items.Should().BeEmpty("clients cannot opt into inactive products");
        (await admin.GetAsync($"/api/v1/products/{hidden.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetFromJsonAsync<PagedResult<ProductDto>>($"/api/v1/products?search={hidden.Name}&includeInactive=true"))!
            .Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Duplicate_product_names_conflict_and_invalid_input_is_400()
    {
        var p = await NewProduct();
        var admin = await Admin();
        (await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest(p.Name, null, null, null, 5m, 1, null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest("Neispravan", null, null, null, -5m, 1, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest("Loš brend", null, 999999, null, 5m, 1, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Stock_adjustment_is_atomic_and_never_goes_negative()
    {
        var p = await NewProduct(stock: 5, threshold: 2);
        var admin = await Admin();

        var up = await admin.PostAsJsonAsync($"/api/v1/products/{p.Id}/stock", new StockAdjustRequest(3));
        (await up.Content.ReadFromJsonAsync<ProductDto>())!.Stock.Should().Be(8);

        (await admin.PostAsJsonAsync($"/api/v1/products/{p.Id}/stock", new StockAdjustRequest(-9))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var down = await admin.PostAsJsonAsync($"/api/v1/products/{p.Id}/stock", new StockAdjustRequest(-6));
        var after = (await down.Content.ReadFromJsonAsync<ProductDto>())!;
        after.Stock.Should().Be(2);
        after.IsLowStock.Should().BeTrue();

        (await admin.PostAsJsonAsync("/api/v1/products/999999/stock", new StockAdjustRequest(1))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?lowStock=true&pageSize=100"))!
            .Items.Should().Contain(x => x.Id == p.Id);
    }

    [Fact]
    public async Task Update_changes_the_product_but_not_its_stock()
    {
        var p = await NewProduct(stock: 7);
        var admin = await Admin();
        var res = await admin.PutAsJsonAsync($"/api/v1/products/{p.Id}", new ProductUpsertRequest(p.Name, "novi opis", null, null, 12.5m, 999, 3));
        var dto = (await res.Content.ReadFromJsonAsync<ProductDto>())!;
        dto.Price.Should().Be(12.5m);
        dto.Description.Should().Be("novi opis");
        dto.Stock.Should().Be(7, "stock only changes through orders and the atomic adjust endpoint");
    }

    [Fact]
    public async Task Brands_and_categories_crud_and_deleting_keeps_the_products()
    {
        var admin = await Admin();
        var brand = (await (await admin.PostAsJsonAsync("/api/v1/brands", new NameRequest($"Brend {Guid.NewGuid():N}"))).Content.ReadFromJsonAsync<BrandDto>())!;
        var category = (await (await admin.PostAsJsonAsync("/api/v1/categories", new NameRequest($"Kat {Guid.NewGuid():N}", "opis"))).Content.ReadFromJsonAsync<CategoryDto>())!;
        (await admin.PostAsJsonAsync("/api/v1/brands", new NameRequest(brand.Name))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var created = await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest($"P {Guid.NewGuid():N}", null, brand.Id, category.Id, 9m, 1, null));
        var product = (await created.Content.ReadFromJsonAsync<ProductDto>())!;
        product.BrandName.Should().Be(brand.Name);
        product.CategoryName.Should().Be(category.Name);

        (await admin.DeleteAsync($"/api/v1/brands/{brand.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = (await admin.GetFromJsonAsync<ProductDto>($"/api/v1/products/{product.Id}"))!;
        after.BrandId.Should().BeNull();
        after.CategoryId.Should().Be(category.Id);
        (await admin.DeleteAsync($"/api/v1/brands/{brand.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------ ordering

    [Fact]
    public async Task The_server_sets_prices_and_totals_and_takes_stock()
    {
        var p = await NewProduct(price: 12.34m, stock: 10);
        var (clientId, client) = await NewClient();

        var res = await Order(client, p, qty: 3, PaymentMethod.OnReceipt);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var o = await Dto(res);
        o.ClientId.Should().Be(clientId);
        o.Status.Should().Be(OrderStatus.Placed);
        o.TotalPrice.Should().Be(37.02m);
        o.Items.Should().ContainSingle().Which.Should().Match<OrderItemDto>(i => i.UnitPrice == 12.34m && i.Quantity == 3 && i.LineTotal == 37.02m);
        o.PaymentClientSecret.Should().BeNull();
        (await StockOf(p)).Should().Be(7);
    }

    [Fact]
    public async Task A_card_order_is_pending_holds_stock_and_gets_a_payment_for_the_converted_amount()
    {
        var p = await NewProduct(price: 19.56m, stock: 5);
        var (_, client) = await NewClient();

        var o = await Dto(await Order(client, p, qty: 2));

        o.Status.Should().Be(OrderStatus.PendingPayment);
        o.TotalPrice.Should().Be(39.12m);
        o.PaymentClientSecret.Should().NotBeNullOrEmpty();
        o.PaymentStatus.Should().Be(PaymentStatus.Pending);
        o.ExpiresUtc.Should().BeAfter(DateTime.UtcNow);
        (await StockOf(p)).Should().Be(3);

        var gateway = api.Services.GetRequiredService<OfflinePaymentGateway>();
        gateway.Created.Should().Contain(c => c.OrderId == o.Id && c.Currency == "eur" && c.AmountMinor == 2000); // 39.12 BAM
    }

    [Fact]
    public async Task Missing_inactive_or_too_many_items_are_rejected_without_touching_stock()
    {
        var p = await NewProduct(stock: 2);
        var gone = await NewProduct(active: false);
        var (_, client) = await NewClient();

        (await Order(client, p, qty: 3, PaymentMethod.OnReceipt)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Order(client, gone)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest([new OrderLineRequest(999999, 1)], FulfilmentType.Pickup, PaymentMethod.Card)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest([], FulfilmentType.Pickup, PaymentMethod.Card)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StockOf(p)).Should().Be(2);
    }

    [Fact]
    public async Task A_multi_line_order_is_all_or_nothing()
    {
        var plenty = await NewProduct(stock: 10);
        var scarce = await NewProduct(stock: 1);
        var (_, client) = await NewClient();

        var res = await client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(
            [new OrderLineRequest(plenty.Id, 2), new OrderLineRequest(scarce.Id, 2)], FulfilmentType.Pickup, PaymentMethod.OnReceipt));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StockOf(plenty)).Should().Be(10, "the first line's decrement must roll back with the failed second line");
        (await StockOf(scarce)).Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_lines_for_one_product_are_merged()
    {
        var p = await NewProduct(price: 5m, stock: 10);
        var (_, client) = await NewClient();
        var res = await client.PostAsJsonAsync("/api/v1/orders", new CreateOrderRequest(
            [new OrderLineRequest(p.Id, 2), new OrderLineRequest(p.Id, 3)], FulfilmentType.Pickup, PaymentMethod.OnReceipt));
        var o = await Dto(res);
        o.Items.Should().ContainSingle().Which.Quantity.Should().Be(5);
        o.TotalPrice.Should().Be(25m);
        (await StockOf(p)).Should().Be(5);
    }

    [Fact]
    public async Task Fifteen_buyers_of_the_last_piece_produce_exactly_one_winner()
    {
        var p = await NewProduct(stock: 1);
        var clients = new List<HttpClient>();
        for (var i = 0; i < 15; i++) clients.Add((await NewClient()).Http);

        var results = await Task.WhenAll(clients.Select(c => Order(c, p, 1, PaymentMethod.OnReceipt)));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(14);
        (await StockOf(p)).Should().Be(0);
    }

    [Fact]
    public async Task Concurrent_orders_never_oversell_when_stock_is_limited()
    {
        var p = await NewProduct(stock: 7);
        var clients = new List<HttpClient>();
        for (var i = 0; i < 12; i++) clients.Add((await NewClient()).Http);

        var results = await Task.WhenAll(clients.Select(c => Order(c, p, 2, PaymentMethod.OnReceipt)));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(3);   // 3 x 2 = 6 of 7
        (await StockOf(p)).Should().Be(1);
    }

    [Fact]
    public async Task Low_stock_event_is_queued_when_an_order_crosses_the_threshold()
    {
        var p = await NewProduct(stock: 4, threshold: 3);
        var (_, client) = await NewClient();
        (await Order(client, p, 2, PaymentMethod.OnReceipt)).StatusCode.Should().Be(HttpStatusCode.Created);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.OutboxMessages.AnyAsync(m => m.Type.EndsWith("ProductLowStock") && m.PayloadJson.Contains(p.Name))).Should().BeTrue();
    }

    [Fact]
    public async Task Delivery_is_not_offered_only_pickup_at_the_salon()
    {
        var p = await NewProduct();
        var (_, client) = await NewClient();
        (await Order(client, p, 1, PaymentMethod.Card, FulfilmentType.Delivery, "Maršala Tita 5")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Order(client, p, 1, PaymentMethod.Card)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Payment_config_is_for_signed_in_users_and_says_cards_are_off_without_keys()
    {
        (await api.CreateClient().GetAsync("/api/v1/payments/config")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (_, client) = await NewClient();
        var cfg = (await client.GetFromJsonAsync<PaymentConfigDto>("/api/v1/payments/config"))!;
        cfg.CardsEnabled.Should().BeFalse();
        cfg.PublishableKey.Should().BeNull();
    }

    [Fact]
    public async Task Only_clients_place_orders_and_anonymous_users_cannot()
    {
        var p = await NewProduct();
        (await Order(api.CreateClient(), p)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Order(await Admin(), p)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Order(await Employee(), p)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ visibility

    [Fact]
    public async Task Orders_are_private_to_their_owner_and_visible_to_admins_only()
    {
        var p = await NewProduct(stock: 5);
        var (_, owner) = await NewClient();
        var (_, other) = await NewClient();
        var o = await Dto(await Order(owner, p, 1, PaymentMethod.OnReceipt));

        (await owner.GetAsync($"/api/v1/orders/{o.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.GetAsync($"/api/v1/orders/{o.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/v1/orders/{o.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await (await Employee()).GetAsync($"/api/v1/orders/{o.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await other.GetFromJsonAsync<PagedResult<OrderDto>>("/api/v1/orders"))!.Items.Should().NotContain(x => x.Id == o.Id);
        var admin = await Admin();
        var asAdmin = (await admin.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;
        asAdmin.ClientName.Should().NotBeNullOrEmpty();
        (await admin.GetFromJsonAsync<PagedResult<OrderDto>>($"/api/v1/orders?clientId={o.ClientId}"))!.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task The_payment_secret_is_never_in_lists_or_for_admins()
    {
        var p = await NewProduct();
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p));

        (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!.PaymentClientSecret.Should().NotBeNullOrEmpty();
        (await client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/v1/orders"))!.Items.Should().OnlyContain(x => x.PaymentClientSecret == null);
        (await (await Admin()).GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!.PaymentClientSecret.Should().BeNull();
    }

    // ------------------------------------------------------------------ cancel / fulfil

    [Fact]
    public async Task Cancelling_returns_the_stock_once()
    {
        var p = await NewProduct(stock: 5);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p, 3, PaymentMethod.OnReceipt));
        (await StockOf(p)).Should().Be(2);

        var cancelled = await client.PostAsync($"/api/v1/orders/{o.Id}/cancel", null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Dto(cancelled)).Status.Should().Be(OrderStatus.Cancelled);
        (await StockOf(p)).Should().Be(5);

        (await client.PostAsync($"/api/v1/orders/{o.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StockOf(p)).Should().Be(5, "a second cancel must not return the stock again");
    }

    [Fact]
    public async Task Concurrent_cancels_return_the_stock_exactly_once()
    {
        var p = await NewProduct(stock: 5);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p, 4, PaymentMethod.OnReceipt));
        var admin = await Admin();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            (i % 2 == 0 ? client : admin).PostAsync($"/api/v1/orders/{o.Id}/cancel", null)));

        results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        (await StockOf(p)).Should().Be(5);
    }

    [Fact]
    public async Task Fulfilment_is_admin_only_and_needs_a_placed_order()
    {
        var p = await NewProduct(stock: 5);
        var (_, client) = await NewClient();
        var admin = await Admin();
        var pending = await Dto(await Order(client, p, 1, PaymentMethod.Card));
        var placed = await Dto(await Order(client, p, 1, PaymentMethod.OnReceipt));

        (await client.PostAsync($"/api/v1/orders/{placed.Id}/fulfil", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/v1/orders/{pending.Id}/fulfil", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var done = await admin.PostAsync($"/api/v1/orders/{placed.Id}/fulfil", null);
        done.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Dto(done)).Status.Should().Be(OrderStatus.Fulfilled);

        (await client.PostAsync($"/api/v1/orders/{placed.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/fulfil", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_client_may_not_cancel_a_paid_order_but_the_admin_can_and_it_is_refunded()
    {
        var p = await NewProduct(price: 10m, stock: 5);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p, 1));
        var intent = await RefOf(o.Id);
        (await Webhook(Event("payment_intent.succeeded", intent, 511))).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsync($"/api/v1/orders/{o.Id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var admin = await Admin();
        var res = await admin.PostAsync($"/api/v1/orders/{o.Id}/cancel", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Dto(res)).PaymentStatus.Should().Be(PaymentStatus.Refunded);
        api.Services.GetRequiredService<OfflinePaymentGateway>().Refunds.Should().Contain(intent);
        (await StockOf(p)).Should().Be(5);
    }

    // ------------------------------------------------------------------ webhook

    [Fact]
    public async Task A_signed_matching_payment_marks_the_order_placed()
    {
        var p = await NewProduct(price: 10m, stock: 5);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p));

        var res = await Webhook(Event("payment_intent.succeeded", await RefOf(o.Id), 511)); // 10 BAM = 5.11 EUR

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;
        after.Status.Should().Be(OrderStatus.Placed);
        after.PaymentStatus.Should().Be(PaymentStatus.Succeeded);
        after.PaidAtUtc.Should().NotBeNull();
        after.PaymentClientSecret.Should().BeNull("a paid order has nothing left to confirm");
    }

    [Fact]
    public async Task A_replayed_webhook_changes_nothing_and_is_acknowledged()
    {
        var p = await NewProduct(price: 10m, stock: 5);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p));
        var body = Event("payment_intent.succeeded", await RefOf(o.Id), 511);

        (await Webhook(body)).StatusCode.Should().Be(HttpStatusCode.OK);
        var first = (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;
        (await Webhook(body)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Webhook(body)).StatusCode.Should().Be(HttpStatusCode.OK);
        var again = (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;

        again.PaidAtUtc.Should().Be(first.PaidAtUtc);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.OutboxMessages.CountAsync(m => m.Type.EndsWith("OrderPaid") && m.PayloadJson.Contains($"\"OrderId\":{o.Id},")))
            .Should().Be(1, "the order is announced as paid only once");
    }

    [Fact]
    public async Task Unsigned_badly_signed_and_stale_webhooks_are_refused_and_change_nothing()
    {
        var p = await NewProduct(price: 10m, stock: 5);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p));
        var body = Event("payment_intent.succeeded", await RefOf(o.Id), 511);

        (await Webhook(body, "t=1,v1=abc")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Webhook(body, StripeWebhook.Header(body, "whsec_wrong", DateTimeOffset.UtcNow))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Webhook(body, StripeWebhook.Header(body, ApiFixture.WebhookSecret, DateTimeOffset.UtcNow.AddHours(-2)))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var noHeader = await api.CreateClient().PostAsync("/api/v1/payments/webhook", new StringContent(body, Encoding.UTF8, "application/json"));
        noHeader.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!.Status.Should().Be(OrderStatus.PendingPayment);
    }

    [Fact]
    public async Task A_payment_for_the_wrong_amount_or_currency_is_rejected_and_the_order_stays_unpaid()
    {
        var p = await NewProduct(price: 10m, stock: 5);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p));
        var intent = await RefOf(o.Id);

        (await Webhook(Event("payment_intent.succeeded", intent, 100))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Webhook(Event("payment_intent.succeeded", intent, 511, "usd"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var after = (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;
        after.Status.Should().Be(OrderStatus.PendingPayment);
        after.PaymentStatus.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task Unknown_intents_and_unrelated_events_are_acknowledged_so_they_are_not_retried()
    {
        (await Webhook(Event("payment_intent.succeeded", "pi_not_ours", 100))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Webhook("""{"id":"evt_x","type":"customer.created","data":{"object":{}}}""")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Webhook("not json at all")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_failed_payment_is_recorded_but_the_order_stays_open_for_a_retry()
    {
        var p = await NewProduct(price: 10m, stock: 5);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p));

        (await Webhook(Event("payment_intent.payment_failed", await RefOf(o.Id), 511))).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;
        after.Status.Should().Be(OrderStatus.PendingPayment);
        after.PaymentStatus.Should().Be(PaymentStatus.Failed);
    }

    // ------------------------------------------------------------------ expiry

    [Fact]
    public async Task Unpaid_card_orders_expire_and_release_their_stock()
    {
        var p = await NewProduct(stock: 4);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p, 3));
        (await StockOf(p)).Should().Be(1);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.Orders.Where(x => x.Id == o.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresUtc, DateTime.UtcNow.AddMinutes(-1)));
            var closed = await scope.ServiceProvider.GetRequiredService<IOrderService>().ExpireUnpaidAsync(default);
            closed.Should().BeGreaterThanOrEqualTo(1);
        }

        var after = (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;
        after.Status.Should().Be(OrderStatus.Expired);
        (await StockOf(p)).Should().Be(4);
    }

    [Fact]
    public async Task Orders_not_yet_due_and_paid_orders_are_not_expired()
    {
        var fresh = await NewProduct(stock: 4);
        var paidP = await NewProduct(price: 10m, stock: 4);
        var (_, client) = await NewClient();
        var notDue = await Dto(await Order(client, fresh, 1));
        var paid = await Dto(await Order(client, paidP, 1));
        (await Webhook(Event("payment_intent.succeeded", await RefOf(paid.Id), 511))).StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.Orders.Where(x => x.Id == paid.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresUtc, DateTime.UtcNow.AddMinutes(-5)));
            await scope.ServiceProvider.GetRequiredService<IOrderService>().ExpireUnpaidAsync(default);
        }

        (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{notDue.Id}"))!.Status.Should().Be(OrderStatus.PendingPayment);
        (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{paid.Id}"))!.Status.Should().Be(OrderStatus.Placed);
    }

    [Fact]
    public async Task A_payment_that_arrives_after_the_order_expired_is_refunded_not_lost()
    {
        var p = await NewProduct(price: 10m, stock: 4);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p, 1));
        var intent = await RefOf(o.Id);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.Orders.Where(x => x.Id == o.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresUtc, DateTime.UtcNow.AddMinutes(-1)));
            await scope.ServiceProvider.GetRequiredService<IOrderService>().ExpireUnpaidAsync(default);
        }

        (await Webhook(Event("payment_intent.succeeded", intent, 511))).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;
        after.Status.Should().Be(OrderStatus.Expired, "the stock was already released; the order must not come back to life");
        after.PaymentStatus.Should().Be(PaymentStatus.Refunded);
        api.Services.GetRequiredService<OfflinePaymentGateway>().Refunds.Should().Contain(intent);
    }

    /// <summary>A processor that is down for refunds.</summary>
    private sealed class RefundsDownGateway : IPaymentGateway
    {
        public Task<PaymentIntentResult> CreateIntentAsync(int orderId, long amountMinor, string currency, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task RefundAsync(string providerRef, CancellationToken ct) => throw new HttpRequestException("Stripe returned 500");
        public Task CancelIntentAsync(string providerRef, CancellationToken ct) => Task.CompletedTask;
    }

    private async Task<(OrderDto Order, string Intent, HttpClient Client)> ExpiredCardOrder()
    {
        var p = await NewProduct(price: 10m, stock: 4);
        var (_, client) = await NewClient();
        var o = await Dto(await Order(client, p, 1));
        var intent = await RefOf(o.Id);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        await db.Orders.Where(x => x.Id == o.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresUtc, DateTime.UtcNow.AddMinutes(-1)));
        await scope.ServiceProvider.GetRequiredService<IOrderService>().ExpireUnpaidAsync(default);
        return (o, intent, client);
    }

    /// <summary>Delivers a signed event to a handler whose refunds fail.</summary>
    private async Task<WebhookOutcome> DeliverWhileRefundsAreDown(string body)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var refunds = ActivatorUtilities.CreateInstance<PendingRefunds>(sp, new RefundsDownGateway());
        var handler = ActivatorUtilities.CreateInstance<PaymentWebhookHandler>(sp, refunds);
        return await handler.HandleAsync(body, StripeWebhook.Header(body, ApiFixture.WebhookSecret, DateTimeOffset.UtcNow), default);
    }

    [Fact]
    public async Task A_late_payment_whose_refund_fails_stays_refund_pending_and_the_webhook_retry_refunds_it()
    {
        var (o, intent, client) = await ExpiredCardOrder();
        var body = Event("payment_intent.succeeded", intent, 511);

        (await DeliverWhileRefundsAreDown(body)).Should().Be(WebhookOutcome.RetryLater, "Stripe must deliver the event again");
        var pending = (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!;
        pending.Status.Should().Be(OrderStatus.Expired);
        pending.PaymentStatus.Should().Be(PaymentStatus.RefundPending, "the refund is owed and must not be forgotten");

        // Stripe retries the same event; this time the refund goes through.
        (await Webhook(body)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!.PaymentStatus.Should().Be(PaymentStatus.Refunded);
        api.Services.GetRequiredService<OfflinePaymentGateway>().Refunds.Should().Contain(intent);
    }

    [Fact]
    public async Task A_refund_that_failed_is_finished_by_the_background_retry()
    {
        var (o, intent, client) = await ExpiredCardOrder();
        (await DeliverWhileRefundsAreDown(Event("payment_intent.succeeded", intent, 511))).Should().Be(WebhookOutcome.RetryLater);

        await using (var scope = api.Services.CreateAsyncScope())
            (await scope.ServiceProvider.GetRequiredService<IOrderService>().RetryPendingRefundsAsync(default)).Should().BeGreaterThanOrEqualTo(1);

        (await client.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{o.Id}"))!.PaymentStatus.Should().Be(PaymentStatus.Refunded);
        api.Services.GetRequiredService<OfflinePaymentGateway>().Refunds.Should().Contain(intent);
    }

    [Fact]
    public async Task A_client_cannot_hoard_stock_with_unpaid_card_orders()
    {
        var p = await NewProduct(stock: 50);
        var (_, client) = await NewClient();
        for (var i = 0; i < 5; i++) (await Order(client, p)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await Order(client, p)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Order(client, p, 1, PaymentMethod.OnReceipt)).StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "paying at pickup holds stock just the same");
    }

    [Fact]
    public async Task Simultaneous_orders_cannot_push_a_client_past_the_unpaid_order_cap()
    {
        var p = await NewProduct(stock: 50);
        var (_, client) = await NewClient();
        for (var i = 0; i < 4; i++) (await Order(client, p)).StatusCode.Should().Be(HttpStatusCode.Created);

        var results = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(i => Order(client, p, 1, i % 2 == 0 ? PaymentMethod.Card : PaymentMethod.OnReceipt)));

        var codes = string.Join(",", results.Select(r => (int)r.StatusCode));
        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1, codes);
        results.Count(r => r.StatusCode == HttpStatusCode.BadRequest).Should().Be(5, codes);
        (await StockOf(p)).Should().Be(45, "refused orders took nothing off the shelf");
    }

    [Fact]
    public async Task Pay_at_pickup_orders_cannot_hold_the_whole_shelf_either()
    {
        var p = await NewProduct(stock: 100);
        var (_, client) = await NewClient();
        for (var i = 0; i < 2; i++)
            (await Order(client, p, 20, PaymentMethod.OnReceipt)).StatusCode.Should().Be(HttpStatusCode.Created);

        // 40 held; another 20 would pass the unit cap.
        (await Order(client, p, 20, PaymentMethod.OnReceipt)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Order(client, p, 10, PaymentMethod.OnReceipt)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await StockOf(p)).Should().Be(50);

        // Collected (fulfilled) orders no longer count.
        var admin = await Admin();
        var mine = (await client.GetFromJsonAsync<PagedResult<OrderDto>>("/api/v1/orders?pageSize=10"))!.Items;
        (await admin.PostAsync($"/api/v1/orders/{mine[0].Id}/fulfil", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Order(client, p, 5, PaymentMethod.OnReceipt)).StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
