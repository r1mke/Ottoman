using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Audit;
using Ottoman.Application.Common;
using Ottoman.Application.Shop;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Outbox;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class AuditTests(ApiFixture api)
{
    private async Task<(HttpClient Http, string Id)> Admin()
    {
        var auth = await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword);
        return (api.CreateClient().As(auth), auth.User.Id);
    }

    private async Task<List<AuditEntryDto>> Entries(HttpClient admin, string search) =>
        (await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>($"/api/v1/audit?pageSize=100&search={Uri.EscapeDataString(search)}"))!.Items.ToList();

    private static ProductUpsertRequest Product(string name) => new(name, "opis", null, null, 10m, 5, null, true);

    [Fact]
    public async Task A_staff_write_leaves_a_line_with_who_what_and_which_record()
    {
        var (admin, adminId) = await Admin();
        var name = $"Audit proizvod {Guid.NewGuid():N}";

        var created = await admin.PostAsJsonAsync("/api/v1/products", Product(name));
        var product = (await created.Content.ReadFromJsonAsync<ProductDto>())!;
        (await admin.PostAsJsonAsync($"/api/v1/products/{product.Id}/stock", new { delta = 2 })).EnsureSuccessStatusCode();

        var lines = await Entries(admin, $"id={product.Id}");

        lines.Should().Contain(e => e.Action == "Products.AdjustStock" && e.Method == "POST" && e.UserId == adminId
                                    && e.Role == "Admin" && e.Target == $"id={product.Id}" && e.StatusCode == 200);
        lines.Single(e => e.Action == "Products.AdjustStock").UserName.Should().NotBeNullOrWhiteSpace();
        lines.Single(e => e.Action == "Products.AdjustStock").Route.Should().Contain("products/{id");
        (await Entries(admin, $"id={product.Id}")).Should().Contain(e => e.Action == "Products.Create" && e.Target == $"id={product.Id}", "a create is traceable to the record it made");
        var all = await Entries(admin, "Products.Create");
        all.Should().Contain(e => e.StatusCode == 201, "the create itself is recorded with its real status");
    }

    [Fact]
    public async Task The_request_body_is_never_stored()
    {
        var (admin, _) = await Admin();
        var secret = $"tajni-opis-{Guid.NewGuid():N}";
        var name = $"Audit tijelo {Guid.NewGuid():N}";
        await admin.PostAsJsonAsync("/api/v1/products", Product(name) with { Description = secret });

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var rows = await db.AuditEntries.AsNoTracking().ToListAsync();

        rows.Should().NotContain(e => e.Route.Contains(secret) || e.Target.Contains(secret) || e.Action.Contains(secret));
    }

    [Fact]
    public async Task Reads_failures_and_client_actions_are_not_recorded()
    {
        var (admin, _) = await Admin();
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var before = (await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>("/api/v1/audit?pageSize=1"))!.TotalCount;

        await admin.GetAsync("/api/v1/products");
        await admin.PostAsJsonAsync("/api/v1/products/999999/stock", new { delta = 1 });
        await admin.PostAsJsonAsync("/api/v1/products", Product(""));
        await client.PostAsync("/api/v1/notifications/read-all", null);
        await client.PostAsJsonAsync("/api/v1/devices", new { token = $"tok_{Guid.NewGuid():N}", platform = "android" });

        var after = (await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>("/api/v1/audit?pageSize=1"))!.TotalCount;
        after.Should().Be(before, "nothing above changed anything a staff member did");
    }

    [Fact]
    public async Task Blocking_a_client_is_in_the_trail_with_the_client_id()
    {
        var (admin, adminId) = await Admin();
        var (_, auth) = await api.CreateClient().NewClient();

        (await admin.PostAsync($"/api/v1/clients/{auth.User.Id}/block", null)).EnsureSuccessStatusCode();

        var lines = await Entries(admin, auth.User.Id);
        lines.Should().ContainSingle(e => e.Action == "Clients.Block" && e.UserId == adminId);
    }

    [Fact]
    public async Task An_employee_action_is_recorded_as_an_employee()
    {
        var (admin, _) = await Admin();
        var kerim = await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword);
        var (_, clientAuth) = await api.CreateClient().NewClient();
        int reservationId;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var service = await db.Services.FirstAsync();
            var start = DateTime.UtcNow.AddDays(-30);
            var r = new Reservation
            {
                ClientId = clientAuth.User.Id,
                EmployeeId = kerim.User.Id,
                StartUtc = start,
                EndUtc = start.AddMinutes(30),
                Status = ReservationStatus.Confirmed,
                TotalPrice = 20m,
                TotalDurationMinutes = 30,
                CreatedAtUtc = start.AddDays(-1),
                Items = [new ReservationItem { ServiceId = service.Id, ServiceName = service.Name, Price = 20m, DurationMinutes = 30 }],
            };
            db.Reservations.Add(r);
            await db.SaveChangesAsync();
            reservationId = r.Id;
        }

        var response = await api.CreateClient().As(kerim).PostAsync($"/api/v1/reservations/{reservationId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Entries(admin, $"id={reservationId}")).Should().Contain(e =>
            e.UserId == kerim.User.Id && e.Role == "Employee" && e.Action == "Reservations.Complete");
    }

    [Fact]
    public async Task Only_an_admin_can_read_the_trail_and_nobody_can_change_it()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var barber = api.CreateClient().As(await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword));

        (await api.CreateClient().GetAsync("/api/v1/audit")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await barber.GetAsync("/api/v1/audit")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var (admin, _) = await Admin();
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch })
            (await admin.SendAsync(new HttpRequestMessage(method, "/api/v1/audit/1"))).StatusCode
                .Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task The_trail_can_be_filtered_by_person_and_time_and_is_newest_first()
    {
        var (admin, adminId) = await Admin();
        var name = $"Audit filter {Guid.NewGuid():N}";
        await admin.PostAsJsonAsync("/api/v1/products", Product(name));

        var mine = (await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>($"/api/v1/audit?userId={adminId}&pageSize=50"))!;
        mine.Items.Should().OnlyContain(e => e.UserId == adminId).And.BeInDescendingOrder(e => e.AtUtc);

        var future = DateTime.UtcNow.AddDays(1).ToString("O");
        var none = (await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>($"/api/v1/audit?from={Uri.EscapeDataString(future)}"))!;
        none.Items.Should().BeEmpty();

        var other = (await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>("/api/v1/audit?userId=nobody"))!;
        other.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Old_entries_are_purged_and_recent_ones_stay()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow().UtcDateTime;
        var marker = $"Purge.{Guid.NewGuid():N}";
        var userId = (await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword)).User.Id;
        AuditEntry Entry(string action, DateTime at) => new()
        {
            AtUtc = at,
            UserId = userId,
            UserName = "n",
            Role = "Admin",
            Method = "POST",
            Action = action,
            Route = "r",
            Target = "t",
            StatusCode = 200,
        };
        db.AuditEntries.AddRange(Entry($"{marker}.old", now - AuditPolicy.Retention - TimeSpan.FromDays(1)), Entry($"{marker}.new", now.AddDays(-10)));
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<OutboxCleanup>().PurgeAsync(default);

        var left = await db.AuditEntries.AsNoTracking().Where(a => a.Action.StartsWith(marker)).Select(a => a.Action).ToListAsync();
        left.Should().Equal($"{marker}.new");
    }

    [Fact]
    public async Task A_line_names_its_target_in_words_and_never_shows_only_an_id()
    {
        var (admin, _) = await Admin();
        var name = $"Audit naziv {Guid.NewGuid():N}";
        var product = (await (await admin.PostAsJsonAsync("/api/v1/products", Product(name))).Content.ReadFromJsonAsync<ProductDto>())!;
        (await admin.PostAsJsonAsync($"/api/v1/products/{product.Id}/stock", new { delta = 1 })).EnsureSuccessStatusCode();

        var (_, client) = await api.CreateClient().NewClient();
        (await admin.PostAsJsonAsync($"/api/v1/users/{client.User.Id}/password",
            new Ottoman.Application.Accounts.AdminSetPasswordRequest("Brand_New_Pass1!"))).EnsureSuccessStatusCode();

        (await Entries(admin, $"id={product.Id}")).Should().Contain(e => e.Action == "Products.AdjustStock" && e.TargetLabel == $"Proizvod: {name}");
        (await Entries(admin, client.User.Id)).Should().Contain(e => e.Action == "Users.SetPassword" && e.TargetLabel == $"{client.User.FirstName} {client.User.LastName}".Trim());

        // An entry whose record no longer exists says so instead of showing a number.
        var gone = $"id=9{Random.Shared.Next(100000, 999999)}";
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            db.AuditEntries.Add(new AuditEntry
            {
                AtUtc = DateTime.UtcNow,
                UserId = null,
                UserName = "Sistem",
                Role = "System",
                Method = "PUT",
                Action = "Products.Update",
                Route = "api/v1/products/{id}",
                Target = gone,
                StatusCode = 200,
            });
            await db.SaveChangesAsync();
        }
        (await Entries(admin, gone)).Should().Contain(e => e.Target == gone && e.TargetLabel == "(obrisano)");
    }
}
