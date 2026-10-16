using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Catalog;
using Ottoman.Application.Idempotency;
using Ottoman.Application.Orders;
using Ottoman.Application.Reservations;
using Ottoman.Application.Shop;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Outbox;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class IdempotencyTests(ApiFixture api)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");
    private static int _seq;

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, HttpClient Http)> NewClient()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private static string Key() => $"k-{Guid.NewGuid():N}";

    private async Task<ProductDto> NewProduct(int stock = 10)
    {
        var admin = await Admin();
        var res = await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest($"Idem proizvod {Interlocked.Increment(ref _seq)} {Guid.NewGuid():N}", "x", null, null, 10m, stock, null, true));
        return (await res.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private async Task<int> StockOf(ProductDto p) =>
        (await (await Admin()).GetFromJsonAsync<ProductDto>($"/api/v1/products/{p.Id}"))!.Stock;

    private static Task<HttpResponseMessage> Order(HttpClient http, ProductDto p, int qty, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest([new OrderLineRequest(p.Id, qty)], FulfilmentType.Pickup, PaymentMethod.OnReceipt)),
        };
        if (key is not null) request.Headers.Add(IdempotencyRules.Header, key);
        return http.SendAsync(request);
    }

    // ------------------------------------------------------------------ orders

    [Fact]
    public async Task The_same_order_sent_twice_is_created_once_and_answered_twice()
    {
        var product = await NewProduct(stock: 10);
        var (_, client) = await NewClient();
        var key = Key();

        var first = await Order(client, product, 2, key);
        var second = await Order(client, product, 2, key);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created, "the second caller gets the first answer");
        second.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        first.Headers.Contains("Idempotent-Replayed").Should().BeFalse();
        var a = (await first.Content.ReadFromJsonAsync<OrderDto>())!;
        var b = (await second.Content.ReadFromJsonAsync<OrderDto>())!;
        b.Id.Should().Be(a.Id);
        b.TotalPrice.Should().Be(a.TotalPrice);
        (await StockOf(product)).Should().Be(8, "stock was taken once");
    }

    [Fact]
    public async Task Without_a_key_nothing_changes_and_two_requests_are_two_orders()
    {
        var product = await NewProduct(stock: 10);
        var (_, client) = await NewClient();

        (await Order(client, product, 1, null)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await Order(client, product, 1, null)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await StockOf(product)).Should().Be(8);
    }

    [Fact]
    public async Task The_same_key_with_a_different_order_is_refused_not_silently_answered()
    {
        var product = await NewProduct(stock: 10);
        var (_, client) = await NewClient();
        var key = Key();
        (await Order(client, product, 1, key)).EnsureSuccessStatusCode();

        var other = await Order(client, product, 3, key);

        other.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await StockOf(product)).Should().Be(9, "the different order was not placed either");
    }

    [Fact]
    public async Task A_failed_attempt_is_not_remembered_so_the_key_can_be_used_again()
    {
        var product = await NewProduct(stock: 2);
        var (_, client) = await NewClient();
        var key = Key();

        var tooMany = await Order(client, product, 5, key);
        tooMany.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var fixedUp = await Order(client, product, 1, key);

        fixedUp.StatusCode.Should().Be(HttpStatusCode.Created);
        fixedUp.Headers.Contains("Idempotent-Replayed").Should().BeFalse();
        (await StockOf(product)).Should().Be(1);
    }

    [Fact]
    public async Task Keys_belong_to_the_caller_so_two_people_never_see_each_others_answers()
    {
        var product = await NewProduct(stock: 10);
        var (_, a) = await NewClient();
        var (_, b) = await NewClient();
        var key = Key();

        var orderA = await (await Order(a, product, 1, key)).Content.ReadFromJsonAsync<OrderDto>();
        var orderB = await (await Order(b, product, 1, key)).Content.ReadFromJsonAsync<OrderDto>();

        orderB!.Id.Should().NotBe(orderA!.Id);
        (await StockOf(product)).Should().Be(8);
    }

    [Fact]
    public async Task A_burst_of_identical_requests_creates_exactly_one_order()
    {
        var product = await NewProduct(stock: 20);
        var (_, client) = await NewClient();
        var key = Key();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Order(client, product, 1, key)));

        var created = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        created.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.Conflict);
        var ids = new HashSet<int>();
        foreach (var r in created) ids.Add((await r.Content.ReadFromJsonAsync<OrderDto>())!.Id);
        ids.Should().HaveCount(1, "every answer is about the same single order");
        (await StockOf(product)).Should().Be(19);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("<script>")]
    public async Task A_malformed_key_is_a_400_and_places_nothing(string key)
    {
        var product = await NewProduct(stock: 10);
        var (_, client) = await NewClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new CreateOrderRequest([new OrderLineRequest(product.Id, 1)], FulfilmentType.Pickup, PaymentMethod.OnReceipt)),
        };
        request.Headers.TryAddWithoutValidation(IdempotencyRules.Header, key);

        var res = await client.SendAsync(request);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StockOf(product)).Should().Be(10);
    }

    [Fact]
    public async Task A_key_that_is_too_long_is_refused()
    {
        var product = await NewProduct(stock: 10);
        var (_, client) = await NewClient();

        (await Order(client, product, 1, new string('a', 65))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- dead attempts

    [Fact]
    public async Task A_placeholder_from_a_request_that_died_does_not_block_the_key_forever()
    {
        var product = await NewProduct(stock: 10);
        var (_, client) = await NewClient();
        var key = Key();
        (await Order(client, product, 1, key)).EnsureSuccessStatusCode();

        // Make the record look like an attempt that started ten minutes ago and never finished.
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.IdempotencyRecords.Where(r => r.Key == key)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.StatusCode, (int?)null).SetProperty(r => r.CreatedAtUtc, DateTime.UtcNow.AddMinutes(-10)));
        }

        var retry = await Order(client, product, 1, key);

        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        retry.Headers.Contains("Idempotent-Replayed").Should().BeFalse("nothing finished, so this is a genuine new attempt");
    }

    [Fact]
    public async Task A_running_request_is_reported_as_in_progress_not_run_twice()
    {
        var product = await NewProduct(stock: 10);
        var (_, client) = await NewClient();
        var key = Key();

        // Reproduce the fingerprint the filter computes by letting one request run to completion, then reopening its record.
        (await Order(client, product, 1, key)).EnsureSuccessStatusCode();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.IdempotencyRecords.Where(r => r.Key == key)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.StatusCode, (int?)null).SetProperty(r => r.CreatedAtUtc, DateTime.UtcNow));
        }

        var again = await Order(client, product, 1, key);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await StockOf(product)).Should().Be(9, "the repeat did not run while the first was thought to be running");
    }

    // ------------------------------------------------------------------ reservations

    [Fact]
    public async Task A_booking_sent_twice_is_one_booking()
    {
        var admin = await Admin();
        var kerim = await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword);
        var services = (await admin.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!;
        var (_, client) = await NewClient();
        var start = LocalTime.ToUtc(Days.Next(), new TimeOnly(11, 0), Zone);
        var key = Key();

        HttpRequestMessage Book()
        {
            var r = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reservations")
            {
                Content = JsonContent.Create(new CreateReservationRequest(kerim.User.Id, [services.Single(s => s.Name == "Šišanje").Id], start, null)),
            };
            r.Headers.Add(IdempotencyRules.Header, key);
            return r;
        }

        var first = await client.SendAsync(Book());
        var second = await client.SendAsync(Book());

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.Created, "not a 409 slot-taken: the second is the same booking, answered again");
        second.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        (await second.Content.ReadFromJsonAsync<ReservationDto>())!.Id.Should().Be((await first.Content.ReadFromJsonAsync<ReservationDto>())!.Id);
    }

    // ------------------------------------------------------------------ housekeeping

    [Fact]
    public async Task Old_records_are_purged_and_fresh_ones_stay()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        IdempotencyRecord Record(string suffix, DateTime at) => new()
        {
            UserId = "u",
            Key = $"{marker}-{suffix}",
            Route = "POST x",
            RequestHash = "h",
            StatusCode = 201,
            ResponseBody = "{}",
            CreatedAtUtc = at,
        };
        db.IdempotencyRecords.AddRange(
            Record("old", DateTime.UtcNow - IdempotencyRules.Retention - TimeSpan.FromHours(1)),
            Record("new", DateTime.UtcNow.AddHours(-1)));
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<OutboxCleanup>().PurgeAsync(default);

        var left = await db.IdempotencyRecords.AsNoTracking().Where(r => r.Key.StartsWith(marker)).Select(r => r.Key).ToListAsync();
        left.Should().Equal($"{marker}-new");
    }

    [Fact]
    public void Key_rules_accept_ordinary_ids_and_reject_anything_else()
    {
        IdempotencyRules.IsValidKey(Guid.NewGuid().ToString()).Should().BeTrue();
        IdempotencyRules.IsValidKey("order:2030-01-01_a.b").Should().BeTrue();
        IdempotencyRules.IsValidKey(null).Should().BeFalse();
        IdempotencyRules.IsValidKey("").Should().BeFalse();
        IdempotencyRules.IsValidKey("a b").Should().BeFalse();
        IdempotencyRules.IsValidKey(new string('a', 64)).Should().BeTrue();
        IdempotencyRules.IsValidKey(new string('a', 65)).Should().BeFalse();
        IdempotencyRules.Hash("a").Should().Be(IdempotencyRules.Hash("a")).And.NotBe(IdempotencyRules.Hash("b"));
    }
}
