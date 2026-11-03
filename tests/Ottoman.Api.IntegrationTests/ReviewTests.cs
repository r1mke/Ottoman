using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Catalog;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Application.Reviews;
using Ottoman.Application.Shop;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class ReviewTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, HttpClient Http)> NewClient()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<(string Id, HttpClient Http)> Kerim()
    {
        var auth = await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword);
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<int> ServiceId(string name) =>
        (await (await Admin()).GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Single(s => s.Name == name).Id;

    /// <summary>A finished appointment, inserted directly: the API (rightly) cannot book in the past.</summary>
    private async Task CompletedVisit(string clientId, string employeeId, int serviceId)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var start = DateTime.UtcNow.AddDays(-3);
        db.Reservations.Add(new Reservation
        {
            ClientId = clientId,
            EmployeeId = employeeId,
            StartUtc = start,
            EndUtc = start.AddMinutes(30),
            Status = ReservationStatus.Completed,
            TotalPrice = 25,
            TotalDurationMinutes = 30,
            CreatedAtUtc = start.AddDays(-1),
            Items = [new ReservationItem { ServiceId = serviceId, ServiceName = "Šišanje", Price = 25, DurationMinutes = 30 }],
        });
        await db.SaveChangesAsync();
    }

    private async Task<ProductDto> CollectedProduct(HttpClient client)
    {
        var admin = await Admin();
        var created = await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest($"Recenzirani {Guid.NewGuid():N}", "opis", null, null, 9m, 5, null));
        var product = (await created.Content.ReadFromJsonAsync<ProductDto>())!;
        var order = (await (await client.PostAsJsonAsync("/api/v1/orders", new { items = new[] { new { productId = product.Id, quantity = 1 } }, fulfilment = "Pickup", paymentMethod = "OnReceipt" }))
            .Content.ReadFromJsonAsync<Ottoman.Application.Orders.OrderDto>())!;
        (await admin.PostAsync($"/api/v1/orders/{order.Id}/fulfil", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return product;
    }

    private static Task<HttpResponseMessage> Review(HttpClient http, ReviewTargetType type, string key, int rating, string? comment = null) =>
        http.PostAsJsonAsync("/api/v1/reviews", new CreateReviewRequest(type, key, rating, comment));

    // ------------------------------------------------------------------ who may review

    [Fact]
    public async Task Without_a_finished_visit_a_client_may_not_review_a_barber()
    {
        var (_, client) = await NewClient();
        var (kerim, _) = await Kerim();

        var e = await client.GetFromJsonAsync<EligibilityDto>($"/api/v1/reviews/eligibility?targetType=Employee&targetKey={kerim}");
        e!.CanReview.Should().BeFalse();
        e.Reason.Should().Contain("termina");
        (await Review(client, ReviewTargetType.Employee, kerim, 5)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_product_cannot_be_reviewed_twice_under_another_spelling_of_its_id()
    {
        var (_, client) = await NewClient();
        var product = await CollectedProduct(client);
        var id = product.Id.ToString();

        (await Review(client, ReviewTargetType.Product, id, 5)).StatusCode.Should().Be(HttpStatusCode.Created);
        foreach (var other in new[] { "0" + id, " " + id, "+" + id })
            (await Review(client, ReviewTargetType.Product, other, 1)).StatusCode.Should().Be(HttpStatusCode.BadRequest, other);
        (await Review(client, ReviewTargetType.Product, id, 1)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task After_a_completed_visit_the_client_can_review_the_barber_and_the_service_once()
    {
        var (clientId, client) = await NewClient();
        var (kerim, _) = await Kerim();
        var service = await ServiceId("Šišanje");
        await CompletedVisit(clientId, kerim, service);

        (await client.GetFromJsonAsync<EligibilityDto>($"/api/v1/reviews/eligibility?targetType=Employee&targetKey={kerim}"))!.CanReview.Should().BeTrue();

        var res = await Review(client, ReviewTargetType.Employee, kerim, 5, "Odličan fade");
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await res.Content.ReadFromJsonAsync<ReviewDto>())!;
        dto.Rating.Should().Be(5);
        dto.TargetName.Should().Be("Kerim Begić");
        dto.IsMine.Should().BeTrue();
        dto.AuthorName.Should().MatchRegex(@"\w+ \w\.$", "other clients see first name and initial only");

        (await Review(client, ReviewTargetType.Service, service.ToString(), 4)).StatusCode.Should().Be(HttpStatusCode.Created);

        // one review per target
        (await Review(client, ReviewTargetType.Employee, kerim, 3)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var again = await client.GetFromJsonAsync<EligibilityDto>($"/api/v1/reviews/eligibility?targetType=Employee&targetKey={kerim}");
        again!.CanReview.Should().BeFalse();
        again.ExistingReviewId.Should().Be(dto.Id);
    }

    [Fact]
    public async Task A_cancelled_or_future_visit_does_not_count()
    {
        var (clientId, client) = await NewClient();
        var (kerim, _) = await Kerim();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var start = DateTime.UtcNow.AddDays(-2);
            db.Reservations.Add(new Reservation { ClientId = clientId, EmployeeId = kerim, StartUtc = start, EndUtc = start.AddMinutes(30), Status = ReservationStatus.Cancelled, TotalPrice = 1, TotalDurationMinutes = 30, CreatedAtUtc = start });
            db.Reservations.Add(new Reservation { ClientId = clientId, EmployeeId = kerim, StartUtc = DateTime.UtcNow.AddDays(2), EndUtc = DateTime.UtcNow.AddDays(2).AddMinutes(30), Status = ReservationStatus.Confirmed, TotalPrice = 1, TotalDurationMinutes = 30, CreatedAtUtc = start });
            await db.SaveChangesAsync();
        }

        (await Review(client, ReviewTargetType.Employee, kerim, 5)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_product_can_be_reviewed_only_after_it_was_collected()
    {
        var (_, client) = await NewClient();
        var admin = await Admin();
        var created = await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest($"Nekupljen {Guid.NewGuid():N}", null, null, null, 9m, 5, null));
        var untouched = (await created.Content.ReadFromJsonAsync<ProductDto>())!;
        (await Review(client, ReviewTargetType.Product, untouched.Id.ToString(), 5)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var collected = await CollectedProduct(client);
        (await Review(client, ReviewTargetType.Product, collected.Id.ToString(), 4, "Dobar")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_order_that_was_not_collected_does_not_count()
    {
        var (_, client) = await NewClient();
        var admin = await Admin();
        var created = await admin.PostAsJsonAsync("/api/v1/products", new ProductUpsertRequest($"Samo naručen {Guid.NewGuid():N}", null, null, null, 9m, 5, null));
        var product = (await created.Content.ReadFromJsonAsync<ProductDto>())!;
        await client.PostAsJsonAsync("/api/v1/orders", new { items = new[] { new { productId = product.Id, quantity = 1 } }, fulfilment = "Pickup", paymentMethod = "OnReceipt" });

        (await Review(client, ReviewTargetType.Product, product.Id.ToString(), 5)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_clients_write_reviews_and_input_is_validated()
    {
        var (kerim, barber) = await Kerim();
        var (clientId, client) = await NewClient();
        await CompletedVisit(clientId, kerim, await ServiceId("Šišanje"));

        (await Review(barber, ReviewTargetType.Employee, kerim, 5)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Review(await Admin(), ReviewTargetType.Employee, kerim, 5)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Review(api.CreateClient(), ReviewTargetType.Employee, kerim, 5)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Review(client, ReviewTargetType.Employee, kerim, 0)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Review(client, ReviewTargetType.Employee, kerim, 6)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Review(client, ReviewTargetType.Employee, kerim, 5, new string('x', 1001))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Review(client, ReviewTargetType.Employee, "no-such-employee", 5)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ reading

    [Fact]
    public async Task The_list_and_the_summary_show_the_average_of_visible_reviews()
    {
        var service = await ServiceId("Masaža glave");
        var (a, clientA) = await NewClient();
        var (b, clientB) = await NewClient();
        var (kerim, _) = await Kerim();
        await CompletedVisit(a, kerim, service);
        await CompletedVisit(b, kerim, service);
        (await Review(clientA, ReviewTargetType.Service, service.ToString(), 5)).EnsureSuccessStatusCode();
        (await Review(clientB, ReviewTargetType.Service, service.ToString(), 2, "Moglo bolje")).EnsureSuccessStatusCode();

        var list = (await clientA.GetFromJsonAsync<ReviewListDto>($"/api/v1/reviews?targetType=Service&targetKey={service}"))!;
        list.Count.Should().BeGreaterThanOrEqualTo(2);
        list.Items.Should().Contain(r => r.Comment == "Moglo bolje" && !r.IsMine);
        list.Items.Should().Contain(r => r.Rating == 5 && r.IsMine);
        list.Average.Should().BeInRange(1, 5);

        var summary = (await clientA.GetFromJsonAsync<List<ReviewSummaryDto>>("/api/v1/reviews/summary?targetType=Service"))!;
        summary.Single(s => s.TargetKey == service.ToString()).Count.Should().Be(list.Count);
    }

    // ------------------------------------------------------------------ editing & moderation

    [Fact]
    public async Task The_author_can_edit_and_delete_but_nobody_else_can()
    {
        var (clientId, client) = await NewClient();
        var (_, other) = await NewClient();
        var (kerim, _) = await Kerim();
        await CompletedVisit(clientId, kerim, await ServiceId("Šišanje"));
        var id = (await (await Review(client, ReviewTargetType.Employee, kerim, 3)).Content.ReadFromJsonAsync<ReviewDto>())!.Id;

        (await other.PutAsJsonAsync($"/api/v1/reviews/{id}", new UpdateReviewRequest(1, "hakovan"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/v1/reviews/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var edited = await client.PutAsJsonAsync($"/api/v1/reviews/{id}", new UpdateReviewRequest(5, "Predomislio sam se"));
        (await edited.Content.ReadFromJsonAsync<ReviewDto>())!.Rating.Should().Be(5);

        (await client.DeleteAsync($"/api/v1/reviews/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<EligibilityDto>($"/api/v1/reviews/eligibility?targetType=Employee&targetKey={kerim}"))!.CanReview.Should().BeTrue("they may review again after deleting");
    }

    [Fact]
    public async Task A_hidden_review_disappears_for_clients_and_from_the_average_but_stays_for_admins()
    {
        var (clientId, client) = await NewClient();
        var (_, viewer) = await NewClient();
        var (kerim, _) = await Kerim();
        var admin = await Admin();
        var service = await ServiceId("Uređivanje brade");
        await CompletedVisit(clientId, kerim, service);
        var review = (await (await Review(client, ReviewTargetType.Service, service.ToString(), 1, "Uvredljiv tekst")).Content.ReadFromJsonAsync<ReviewDto>())!;
        var before = (await viewer.GetFromJsonAsync<ReviewListDto>($"/api/v1/reviews?targetType=Service&targetKey={service}"))!;

        (await viewer.PutAsJsonAsync($"/api/v1/reviews/{review.Id}/hide", new HideRequest(true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PutAsJsonAsync($"/api/v1/reviews/{review.Id}/hide", new HideRequest(true))).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = (await viewer.GetFromJsonAsync<ReviewListDto>($"/api/v1/reviews?targetType=Service&targetKey={service}&includeHidden=true"))!;
        after.Items.Should().NotContain(r => r.Id == review.Id, "clients cannot opt into hidden reviews");
        after.Count.Should().Be(before.Count - 1);

        var asAdmin = (await admin.GetFromJsonAsync<ReviewListDto>($"/api/v1/reviews?targetType=Service&targetKey={service}&includeHidden=true"))!;
        asAdmin.Items.Should().Contain(r => r.Id == review.Id && r.IsHidden);
        (await admin.GetFromJsonAsync<PagedResult<ReviewDto>>("/api/v1/reviews/all?onlyHidden=true&pageSize=100"))!.Items.Should().Contain(r => r.Id == review.Id);
    }

    [Fact]
    public async Task The_salon_can_reply_and_the_reply_is_public()
    {
        var (clientId, client) = await NewClient();
        var (kerim, _) = await Kerim();
        var service = await ServiceId("Srednja / duga kosa");
        await CompletedVisit(clientId, kerim, service);
        var review = (await (await Review(client, ReviewTargetType.Service, service.ToString(), 4, "Super")).Content.ReadFromJsonAsync<ReviewDto>())!;

        (await client.PostAsJsonAsync($"/api/v1/reviews/{review.Id}/reply", new ReplyRequest("Hvala!"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await (await Admin()).PostAsJsonAsync($"/api/v1/reviews/{review.Id}/reply", new ReplyRequest("Hvala na povjerenju!"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var list = (await client.GetFromJsonAsync<ReviewListDto>($"/api/v1/reviews?targetType=Service&targetKey={service}"))!;
        list.Items.Single(r => r.Id == review.Id).ReplyText.Should().Be("Hvala na povjerenju!");
    }

    [Fact]
    public async Task Racing_duplicate_reviews_produce_exactly_one()
    {
        var (clientId, client) = await NewClient();
        var (kerim, _) = await Kerim();
        await CompletedVisit(clientId, kerim, await ServiceId("Šišanje"));

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Review(client, ReviewTargetType.Employee, kerim, 5)));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(7);
    }
}
