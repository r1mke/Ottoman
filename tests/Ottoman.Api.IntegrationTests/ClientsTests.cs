using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Clients;
using Ottoman.Application.Common;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class ClientsTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Email, string Id, HttpClient Http)> NewClient()
    {
        var (email, auth) = await api.CreateClient().NewClient();
        return (email, auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<string> KerimId() =>
        (await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword)).User.Id;

    private async Task AddReservation(string clientId, string employeeId, DateTime startUtc, decimal price, ReservationStatus status)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var service = await db.Services.FirstAsync();
        db.Reservations.Add(new Reservation
        {
            ClientId = clientId,
            EmployeeId = employeeId,
            StartUtc = startUtc,
            EndUtc = startUtc.AddMinutes(30),
            Status = status,
            TotalPrice = price,
            TotalDurationMinutes = 30,
            CreatedAtUtc = startUtc.AddDays(-1),
            Items = [new ReservationItem { ServiceId = service.Id, ServiceName = service.Name, Price = price, DurationMinutes = 30 }],
        });
        await db.SaveChangesAsync();
    }

    private async Task AddOrder(string clientId, decimal total, OrderStatus status)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var product = await db.Products.FirstAsync();
        db.Orders.Add(new Order
        {
            ClientId = clientId,
            Status = status,
            Fulfilment = FulfilmentType.Pickup,
            PaymentMethod = PaymentMethod.OnReceipt,
            TotalPrice = total,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
            Items = [new OrderItem { ProductId = product.Id, ProductName = product.Name, UnitPrice = total, Quantity = 1 }],
        });
        await db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------- access

    [Fact]
    public async Task Client_pages_are_for_admins_only()
    {
        var (_, id, client) = await NewClient();
        var barber = api.CreateClient().As(await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword));

        foreach (var (method, url) in new[]
                 {
                     (HttpMethod.Get, "/api/v1/clients"), (HttpMethod.Get, $"/api/v1/clients/{id}"),
                     (HttpMethod.Post, $"/api/v1/clients/{id}/block"), (HttpMethod.Post, $"/api/v1/clients/{id}/unblock"),
                 })
        {
            (await api.CreateClient().SendAsync(new HttpRequestMessage(method, url))).StatusCode.Should().Be(HttpStatusCode.Unauthorized, url);
            (await client.SendAsync(new HttpRequestMessage(method, url))).StatusCode.Should().Be(HttpStatusCode.Forbidden, url);
            (await barber.SendAsync(new HttpRequestMessage(method, url))).StatusCode.Should().Be(HttpStatusCode.Forbidden, url);
        }
    }

    // ---------------------------------------------------------------- detail

    [Fact]
    public async Task Detail_counts_what_really_happened()
    {
        var (_, id, _) = await NewClient();
        var kerim = await KerimId();
        var admin = await Admin();
        var past = DateTime.UtcNow.AddDays(-10);
        await AddReservation(id, kerim, past, 25m, ReservationStatus.Completed);
        await AddReservation(id, kerim, past.AddDays(1), 15m, ReservationStatus.Completed);
        await AddReservation(id, kerim, past.AddDays(2), 99m, ReservationStatus.NoShow);
        await AddReservation(id, kerim, past.AddDays(3), 99m, ReservationStatus.Cancelled);
        await AddReservation(id, kerim, DateTime.UtcNow.AddDays(5), 20m, ReservationStatus.Confirmed);
        await AddOrder(id, 12.5m, OrderStatus.Fulfilled);
        await AddOrder(id, 80m, OrderStatus.Cancelled);

        var detail = (await admin.GetFromJsonAsync<ClientDetail>($"/api/v1/clients/{id}"))!;

        detail.Stats.Visits.Should().Be(2);
        detail.Stats.NoShows.Should().Be(1);
        detail.Stats.Cancelled.Should().Be(1);
        detail.Stats.Upcoming.Should().Be(1);
        detail.Stats.ServicesSpent.Should().Be(40m, "only completed visits count as spent");
        detail.Stats.Orders.Should().Be(1);
        detail.Stats.ProductsSpent.Should().Be(12.5m, "a cancelled order is not money spent");
        detail.Stats.LastVisitUtc.Should().BeCloseTo(past.AddDays(1), TimeSpan.FromSeconds(2));
        detail.Reservations.Should().HaveCount(5).And.BeInDescendingOrder(r => r.StartUtc);
        detail.Reservations.First().EmployeeName.Should().Contain("Kerim");
        detail.Orders.Should().HaveCount(2);
    }

    [Fact]
    public async Task Detail_of_a_new_client_is_empty_and_only_clients_have_one()
    {
        var (email, id, _) = await NewClient();
        var admin = await Admin();

        var detail = (await admin.GetFromJsonAsync<ClientDetail>($"/api/v1/clients/{id}"))!;

        detail.Email.Should().Be(email);
        detail.IsBlocked.Should().BeFalse();
        detail.Stats.Should().Be(new ClientStats(0, 0, 0, 0, 0m, 0, 0m, null));
        detail.Reservations.Should().BeEmpty();
        detail.Orders.Should().BeEmpty();

        (await admin.GetAsync("/api/v1/clients/does-not-exist")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync($"/api/v1/clients/{await KerimId()}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "an employee is not a client");
    }

    // ---------------------------------------------------------------- blocking

    [Fact]
    public async Task Blocking_cuts_off_a_signed_in_client_immediately_and_unblocking_restores_access()
    {
        var (email, id, client) = await NewClient();
        var admin = await Admin();
        (await client.GetAsync("/api/v1/notifications/unread-count")).StatusCode.Should().Be(HttpStatusCode.OK);

        var blocked = await admin.PostAsync($"/api/v1/clients/{id}/block", null);

        blocked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await blocked.Content.ReadFromJsonAsync<ClientListItem>())!.IsBlocked.Should().BeTrue();
        (await client.GetAsync("/api/v1/notifications/unread-count")).StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the access token is still valid for minutes, but the account is not");
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = TestClient.StrongPassword }))
            .StatusCode.Should().NotBe(HttpStatusCode.OK);

        var unblocked = await admin.PostAsync($"/api/v1/clients/{id}/unblock", null);

        unblocked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/notifications/unread-count")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email, password = TestClient.StrongPassword }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Blocking_ends_every_refresh_session()
    {
        var (email, id, _) = await NewClient();
        var session = await api.CreateClient().LoginOk(email, TestClient.StrongPassword);
        var admin = await Admin();

        (await admin.PostAsync($"/api/v1/clients/{id}/block", null)).EnsureSuccessStatusCode();

        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_clients_can_be_blocked_so_the_admin_cannot_lock_the_salon_out()
    {
        var admin = await Admin();
        var kerim = await KerimId();
        var adminId = (await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword)).User.Id;

        (await admin.PostAsync($"/api/v1/clients/{kerim}/block", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsync($"/api/v1/clients/{adminId}/block", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsync("/api/v1/clients/nobody/block", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync("/api/v1/notifications/unread-count")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_list_shows_the_blocked_flag_and_can_be_searched()
    {
        var (email, id, _) = await NewClient();
        var admin = await Admin();
        await admin.PostAsync($"/api/v1/clients/{id}/block", null);

        var page = (await admin.GetFromJsonAsync<PagedResult<ClientListItem>>($"/api/v1/clients?search={Uri.EscapeDataString(email)}"))!;

        page.Items.Should().ContainSingle(c => c.Id == id && c.IsBlocked);
    }
}
