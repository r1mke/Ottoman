using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Catalog;
using Ottoman.Application.Employees;
using Ottoman.Application.Shop;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class RecommendationTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, HttpClient Http)> NewClient()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<ProductDto> ProductNamed(HttpClient http, string name) =>
        (await http.GetFromJsonAsync<Ottoman.Application.Common.PagedResult<ProductDto>>($"/api/v1/products?search={Uri.EscapeDataString(name)}&pageSize=50"))!
            .Items.First(p => p.Name == name);

    private static Task<HttpResponseMessage> Buy(HttpClient http, int productId) =>
        http.PostAsJsonAsync("/api/v1/orders", new { items = new[] { new { productId, quantity = 1 } }, fulfilment = "Pickup", paymentMethod = "OnReceipt" });

    private static async Task<List<T>> Items<T>(HttpClient http, string url) =>
        (await http.GetFromJsonAsync<List<Ottoman.Application.Recommendations.Recommended<T>>>(url))!.Select(r => r.Item).ToList();

    private static Task<List<ProductDto>> Recommended(HttpClient http, int count = 10) =>
        Items<ProductDto>(http, $"/api/v1/recommendations/products?count={count}");

    [Fact]
    public async Task Recommendations_need_a_login()
    {
        (await api.CreateClient().GetAsync("/api/v1/recommendations/products")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await api.CreateClient().GetAsync("/api/v1/products/1/related")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Someone_who_bought_beard_oil_is_offered_the_beard_balm_and_not_the_oil_again()
    {
        var (_, client) = await NewClient();
        var oil = await ProductNamed(client, "Dandy Beard Oil");
        var balm = await ProductNamed(client, "Proraso Beard Balm");
        (await Buy(client, oil.Id)).StatusCode.Should().Be(HttpStatusCode.Created);

        var recs = (await Recommended(client))!;

        recs.Select(p => p.Id).Should().NotContain(oil.Id, "they already own it");
        recs.First().Id.Should().Be(balm.Id, "same category, same use");
    }

    [Fact]
    public async Task Every_recommendation_explains_itself_with_facts_from_the_comparison()
    {
        var (_, client) = await NewClient();
        var oil = await ProductNamed(client, "Dandy Beard Oil");
        var balm = await ProductNamed(client, "Proraso Beard Balm");
        (await Buy(client, oil.Id)).StatusCode.Should().Be(HttpStatusCode.Created);

        var recs = (await client.GetFromJsonAsync<List<Ottoman.Application.Recommendations.Recommended<ProductDto>>>("/api/v1/recommendations/products?count=6"))!;

        var forBalm = recs.Single(r => r.Item.Id == balm.Id).Reason;
        forBalm.Kind.Should().Be("SimilarToInterest");
        forBalm.SourceName.Should().Be(oil.Name);
        forBalm.Text.Should().Contain(oil.Name).And.Contain("Jer ste kupili").And.Contain("ista kategorija");
        recs.Should().OnlyContain(r => r.Reason.Text.Length > 0, "no recommendation is shown without a reason");
    }

    [Fact]
    public async Task A_client_without_history_is_told_the_list_is_about_popularity()
    {
        var (_, client) = await NewClient();

        var recs = (await client.GetFromJsonAsync<List<Ottoman.Application.Recommendations.Recommended<ProductDto>>>("/api/v1/recommendations/products?count=5"))!;

        recs.Should().NotBeEmpty();
        recs.Should().OnlyContain(r => r.Reason.Kind == "Popular" && r.Reason.Text.StartsWith("Popularno u salonu"));
    }

    [Fact]
    public async Task Similar_products_say_what_they_are_similar_to()
    {
        var (_, client) = await NewClient();
        var pomade = await ProductNamed(client, "Vosak za kosu Dandy Shaping Pomade");

        var related = (await client.GetFromJsonAsync<List<Ottoman.Application.Recommendations.Recommended<ProductDto>>>($"/api/v1/products/{pomade.Id}/related?count=3"))!;

        related.Should().NotBeEmpty();
        related.Should().OnlyContain(r => r.Reason.Kind == "SimilarToItem" && r.Reason.Text.StartsWith("Slično kao") && r.Reason.Text.Contains(pomade.Name));
    }

    [Fact]
    public async Task A_new_client_gets_popular_products_that_are_actually_in_stock()
    {
        var (_, client) = await NewClient();
        var admin = await Admin();
        var soldOut = await (await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest($"Rasprodan {Guid.NewGuid():N}", "x", null, null, 5m, 0, null)))
            .Content.ReadFromJsonAsync<ProductDto>();

        var recs = (await Recommended(client, 20))!;

        recs.Should().NotBeEmpty("a cold start falls back to what sells, never to nothing");
        recs.Should().OnlyContain(p => p.Stock > 0 && p.IsActive);
        recs.Select(p => p.Id).Should().NotContain(soldOut!.Id);
    }

    [Fact]
    public async Task Related_products_are_similar_in_stock_and_never_the_product_itself()
    {
        var (_, client) = await NewClient();
        var pomade = await ProductNamed(client, "Vosak za kosu Dandy Shaping Pomade");
        var clay = await ProductNamed(client, "Dandy Matt Clay");

        var related = (await Items<ProductDto>(client, $"/api/v1/products/{pomade.Id}/related?count=4"));

        related.Select(p => p.Id).Should().NotContain(pomade.Id);
        related.Take(2).Select(p => p.Id).Should().Contain(clay.Id);
        related.Should().OnlyContain(p => p.Stock > 0);
    }

    [Fact]
    public async Task A_new_similar_product_shows_up_straight_away_because_the_index_is_invalidated_on_change()
    {
        var (_, client) = await NewClient();
        var admin = await Admin();
        var oil = await ProductNamed(client, "Dandy Beard Oil");
        var token = Guid.NewGuid().ToString("N")[..8];

        var before = (await Items<ProductDto>(client, $"/api/v1/products/{oil.Id}/related?count=20"));
        var name = $"Dandy Beard Wash {token}";
        var created = await admin.PostAsJsonAsync("/api/v1/products",
            new ProductUpsertRequest(name, "Ulje i šampon za bradu", oil.BrandId, oil.CategoryId, 15m, 5, null));
        var wash = (await created.Content.ReadFromJsonAsync<ProductDto>())!;

        before.Select(p => p.Id).Should().NotContain(wash.Id);
        var after = (await Items<ProductDto>(client, $"/api/v1/products/{oil.Id}/related?count=20"));
        after.Select(p => p.Id).Should().Contain(wash.Id);

        // deactivating goes through a bulk update; it must invalidate too
        (await admin.DeleteAsync($"/api/v1/products/{wash.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var gone = (await Items<ProductDto>(client, $"/api/v1/products/{oil.Id}/related?count=20"));
        gone.Select(p => p.Id).Should().NotContain(wash.Id);
    }

    [Fact]
    public async Task A_bad_review_pushes_a_product_family_out_of_the_feed()
    {
        var (clientId, client) = await NewClient();
        var clay = await ProductNamed(client, "Dandy Matt Clay");
        var pomade = await ProductNamed(client, "Vosak za kosu Dandy Shaping Pomade");
        (await Buy(client, clay.Id)).EnsureSuccessStatusCode();
        var neutral = (await Recommended(client))!;
        neutral.Select(p => p.Id).Should().Contain(pomade.Id, "same category and brand as what they bought");

        // collected the clay and hated it
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.Orders.Where(o => o.ClientId == clientId).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Fulfilled));
        }
        (await client.PostAsJsonAsync("/api/v1/reviews", new { targetType = "Product", targetKey = clay.Id.ToString(), rating = 1, comment = "Užas" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var after = (await Recommended(client))!;
        after.Select(p => p.Id).Should().NotContain(clay.Id);
        // the purchase (+1) and the 1-star review (-1) cancel out: the waxes no longer lead
        after.FirstOrDefault()?.Id.Should().NotBe(pomade.Id);
    }

    [Fact]
    public async Task Service_and_barber_recommendations_work_for_new_and_returning_clients()
    {
        var (clientId, client) = await NewClient();
        var services = (await client.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!;
        var sisanje = services.Single(s => s.Name == "Šišanje");
        var kerim = (await client.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!.Single(e => e.FirstName == "Kerim");

        var cold = (await Items<ServiceDto>(client, "/api/v1/recommendations/services?count=3"));
        cold.Should().NotBeEmpty().And.OnlyContain(s => s.IsActive);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var start = DateTime.UtcNow.AddDays(-4);
            db.Reservations.Add(new Reservation
            {
                ClientId = clientId,
                EmployeeId = kerim.Id,
                StartUtc = start,
                EndUtc = start.AddMinutes(30),
                Status = ReservationStatus.Completed,
                TotalPrice = 25,
                TotalDurationMinutes = 30,
                CreatedAtUtc = start.AddDays(-1),
                Items = [new ReservationItem { ServiceId = sisanje.Id, ServiceName = sisanje.Name, Price = 25, DurationMinutes = 30 }],
            });
            await db.SaveChangesAsync();
        }

        var warm = (await Items<ServiceDto>(client, "/api/v1/recommendations/services?count=3"));
        warm.First().Id.Should().Be(sisanje.Id, "a haircut is most like another haircut");

        var barbers = (await Items<EmployeeDto>(client, "/api/v1/recommendations/employees?count=4"));
        barbers.Should().NotBeEmpty().And.OnlyContain(e => e.IsActive);
        barbers.First().Id.Should().Be(kerim.Id, "they have been to Kerim");
        barbers.Should().OnlyContain(e => e.Email == null, "private contact data stays private");
    }

    [Fact]
    public async Task Count_is_clamped()
    {
        var (_, client) = await NewClient();
        (await Recommended(client, 1000))!.Count.Should().BeLessThanOrEqualTo(20);
        (await client.GetAsync("/api/v1/recommendations/products?count=-5")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
