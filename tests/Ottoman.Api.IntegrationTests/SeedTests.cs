using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Reports;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Seeding;

namespace Ottoman.Api.IntegrationTests;

/// <summary>The demo database has to be big and consistent enough to test every screen on a fresh start.</summary>
[Collection("api")]
public class SeedTests(ApiFixture api)
{
    private async Task<T> Query<T>(Func<OttomanDbContext, Task<T>> read)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<OttomanDbContext>());
    }

    [Fact]
    public async Task There_is_enough_of_everything_to_look_at()
    {
        var counts = await Query(async db => new
        {
            Services = await db.Services.CountAsync(),
            Products = await db.Products.CountAsync(),
            Employees = await db.EmployeeProfiles.CountAsync(),
            Brands = await db.Brands.CountAsync(),
            Categories = await db.Categories.CountAsync(),
            Clients = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where r.Name == Roles.Client select ur.UserId).CountAsync(),
            Reservations = await db.Reservations.CountAsync(),
            Orders = await db.Orders.CountAsync(),
            Reviews = await db.Reviews.CountAsync(),
            Announcements = await db.Announcements.CountAsync(),
        });

        counts.Services.Should().BeGreaterThanOrEqualTo(8);
        counts.Products.Should().BeGreaterThanOrEqualTo(10);
        counts.Employees.Should().BeGreaterThanOrEqualTo(3);
        counts.Brands.Should().BeGreaterThanOrEqualTo(3);
        counts.Categories.Should().BeGreaterThanOrEqualTo(5);
        counts.Clients.Should().BeGreaterThanOrEqualTo(9);
        counts.Reservations.Should().BeGreaterThanOrEqualTo(150);
        counts.Orders.Should().BeGreaterThanOrEqualTo(40);
        counts.Reviews.Should().BeGreaterThanOrEqualTo(30);
        counts.Announcements.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task The_history_has_every_kind_of_outcome()
    {
        var statuses = await Query(db => db.Reservations.Select(r => r.Status).Distinct().ToListAsync());
        var orderStatuses = await Query(db => db.Orders.Select(o => o.Status).Distinct().ToListAsync());

        statuses.Should().Contain([ReservationStatus.Completed, ReservationStatus.NoShow, ReservationStatus.Cancelled, ReservationStatus.Confirmed, ReservationStatus.Pending]);
        orderStatuses.Should().Contain([OrderStatus.Fulfilled, OrderStatus.Placed, OrderStatus.Cancelled]);
    }

    [Fact]
    public async Task Bookings_are_consistent_prices_slots_and_cancellation_details()
    {
        var bad = await Query(async db =>
        {
            var reservations = await db.Reservations.AsNoTracking().Include(r => r.Items).Include(r => r.Slots).ToListAsync();
            return new
            {
                WrongTotal = reservations.Count(r => r.TotalPrice != r.Items.Sum(i => i.Price)),
                WrongDuration = reservations.Count(r => r.TotalDurationMinutes != r.Items.Sum(i => i.DurationMinutes)),
                WrongEnd = reservations.Count(r => r.EndUtc != r.StartUtc.AddMinutes(r.TotalDurationMinutes)),
                ActiveWithoutSlots = reservations.Count(r => r.Status != ReservationStatus.Cancelled && r.Slots.Count == 0),
                CancelledWithSlots = reservations.Count(r => r.Status == ReservationStatus.Cancelled && r.Slots.Count > 0),
                CancelledWithoutWho = reservations.Count(r => r.Status == ReservationStatus.Cancelled && r.CancelledBy is null),
                AdminCancelWithoutReason = reservations.Count(r => r.Status == ReservationStatus.Cancelled && r.CancelledBy == "Admin" && string.IsNullOrWhiteSpace(r.CancelReason)),
            };
        });

        bad.WrongTotal.Should().Be(0);
        bad.WrongDuration.Should().Be(0);
        bad.WrongEnd.Should().Be(0);
        bad.ActiveWithoutSlots.Should().Be(0, "every live booking occupies its steps, so it cannot be double booked");
        bad.CancelledWithSlots.Should().Be(0, "a cancelled booking frees its steps");
        bad.CancelledWithoutWho.Should().Be(0);
        bad.AdminCancelWithoutReason.Should().Be(0, "the salon always explains a cancellation");
    }

    [Fact]
    public async Task Upcoming_bookings_leave_the_days_the_tests_and_reviewers_book_on_free()
    {
        var farthest = await Query(db => db.Reservations.Where(r => r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed).MaxAsync(r => r.StartUtc));

        farthest.Should().BeBefore(DateTime.UtcNow.AddDays(9), "seeded future bookings stay inside the next week");
    }

    [Fact]
    public async Task Orders_add_up_and_card_orders_have_a_real_payment_record()
    {
        var bad = await Query(async db =>
        {
            var orders = await db.Orders.AsNoTracking().Include(o => o.Items).ToListAsync();
            var payments = await db.Payments.AsNoTracking().ToDictionaryAsync(p => p.Id);
            return new
            {
                WrongTotal = orders.Count(o => o.TotalPrice != o.Items.Sum(i => i.UnitPrice * i.Quantity)),
                PaidCardWithoutPayment = orders.Count(o => o.PaymentMethod == PaymentMethod.Card && o.PaidAtUtc != null && (o.PaymentId is null || !payments.ContainsKey(o.PaymentId.Value))),
                PaymentAmountDiffers = orders.Count(o => o.PaymentId is { } id && payments.TryGetValue(id, out var p) && p.Amount != o.TotalPrice && o.Items.Count > 0 && p.ProviderRef.StartsWith("pi_demo_")),
                FulfilledWithoutDate = orders.Count(o => o.Status == OrderStatus.Fulfilled && o.FulfilledAtUtc is null),
            };
        });

        bad.WrongTotal.Should().Be(0);
        bad.PaidCardWithoutPayment.Should().Be(0);
        bad.PaymentAmountDiffers.Should().Be(0);
        bad.FulfilledWithoutDate.Should().Be(0);
    }

    [Fact]
    public async Task Every_seeded_review_is_from_someone_who_really_used_the_thing()
    {
        var unverified = await Query(async db =>
        {
            var reviews = await db.Reviews.AsNoTracking().Where(r => r.Comment == null || r.Comment != "").ToListAsync();
            var completed = (await db.Reservations.AsNoTracking().Where(r => r.Status == ReservationStatus.Completed)
                .Select(r => new { r.ClientId, r.EmployeeId, Services = r.Items.Select(i => i.ServiceId).ToList() }).ToListAsync());
            var fulfilled = (await db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Fulfilled)
                .Select(o => new { o.ClientId, Products = o.Items.Select(i => i.ProductId).ToList() }).ToListAsync());

            return reviews.Count(r => r.TargetType switch
            {
                ReviewTargetType.Employee => !completed.Any(c => c.ClientId == r.ClientId && c.EmployeeId == r.TargetKey),
                ReviewTargetType.Service => !completed.Any(c => c.ClientId == r.ClientId && c.Services.Contains(int.Parse(r.TargetKey))),
                ReviewTargetType.Product => !fulfilled.Any(o => o.ClientId == r.ClientId && o.Products.Contains(int.Parse(r.TargetKey))),
                _ => true,
            });
        });

        unverified.Should().Be(0, "ratings are only possible after a completed visit or a collected order");
    }

    [Fact]
    public async Task The_dashboard_has_real_numbers_for_the_last_month()
    {
        var admin = api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-30);

        var dashboard = (await admin.GetFromJsonAsync<DashboardDto>($"/api/v1/reports/dashboard?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"))!;

        dashboard.Revenue.Value.Should().BeGreaterThan(0);
        dashboard.ServicesRevenue.Value.Should().BeGreaterThan(0);
        dashboard.ProductsRevenue.Value.Should().BeGreaterThan(0);
        dashboard.Appointments.Value.Should().BeGreaterThan(20);
        dashboard.TopServices.Should().NotBeEmpty();
        dashboard.Employees.Should().HaveCountGreaterThanOrEqualTo(3);
        dashboard.Series.Sum(s => s.Total).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Seeding_again_changes_nothing()
    {
        var before = await Query(async db => (await db.Reservations.CountAsync(), await db.Orders.CountAsync(), await db.Services.CountAsync()));

        await using (var scope = api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();

        var after = await Query(async db => (await db.Reservations.CountAsync(), await db.Orders.CountAsync(), await db.Services.CountAsync()));
        after.Should().Be(before);
    }

    [Fact]
    public void The_placeholder_picture_is_a_valid_distinct_png_that_the_upload_rules_accept()
    {
        var a = PlaceholderImage.Create("Dandy Pomade");
        var b = PlaceholderImage.Create("Masaža glave");

        Ottoman.Application.Images.ImageRules.Detect(a).Should().Be("image/png");
        Ottoman.Application.Images.ImageRules.Check(a.Length, a, new Ottoman.Application.Images.ImageOptions().MaxBytes).Should().BeNull();
        Ottoman.Application.Images.ImageRules.IsIntact(a).Should().BeTrue("the structural upload check accepts it too");
        a.Length.Should().BeLessThan(150_000, "small enough to upload quickly on every start");
        a.Should().Equal(PlaceholderImage.Create("Dandy Pomade"), "the same name always gives the same picture");
        a.Should().NotEqual(b, "different things look different");

        // Every PNG chunk carries a valid CRC, so strict decoders accept it too.
        var offset = 8;
        var chunks = new List<string>();
        while (offset < a.Length)
        {
            var length = (a[offset] << 24) | (a[offset + 1] << 16) | (a[offset + 2] << 8) | a[offset + 3];
            var body = a.AsSpan(offset + 4, 4 + length).ToArray();
            var stored = (uint)((a[offset + 8 + length] << 24) | (a[offset + 9 + length] << 16) | (a[offset + 10 + length] << 8) | a[offset + 11 + length]);
            Crc32(body).Should().Be(stored);
            chunks.Add(System.Text.Encoding.ASCII.GetString(body, 0, 4));
            offset += 12 + length;
        }
        chunks.Should().Equal("IHDR", "IDAT", "IEND");
    }

    [Fact]
    public async Task Pictures_are_seeded_once_for_everything_that_has_none_when_an_account_is_configured()
    {
        var store = new Ottoman.Infrastructure.Images.OfflineImageStore();
        var configured = Microsoft.Extensions.Options.Options.Create(new Ottoman.Application.Images.ImageOptions { CloudName = "c", ApiKey = "k", ApiSecret = "s" });
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var seeder = new DemoImageSeeder(db, store, configured, Microsoft.Extensions.Logging.Abstractions.NullLogger<DemoImageSeeder>.Instance);

        await seeder.SeedAsync();
        var uploaded = store.Uploaded.Count;
        await seeder.SeedAsync();

        uploaded.Should().BeGreaterThan(20, "products, services and barbers all got a picture");
        store.Uploaded.Count.Should().Be(uploaded, "the second run found nothing left to do");
        (await db.Products.CountAsync(p => !db.ProductImages.Any(i => i.ProductId == p.Id))).Should().Be(0);
        (await db.Services.CountAsync(s => s.ImageKey == null)).Should().Be(0);
        (await db.EmployeeProfiles.CountAsync(e => e.PhotoKey == null)).Should().Be(0);
        store.Uploaded.Should().Contain(k => k.StartsWith("ottoman/products/"));
    }

    [Fact]
    public async Task Without_an_account_nothing_is_uploaded_and_a_broken_one_never_stops_the_start()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var unconfigured = Microsoft.Extensions.Options.Options.Create(new Ottoman.Application.Images.ImageOptions());
        var store = new Ottoman.Infrastructure.Images.OfflineImageStore();

        await new DemoImageSeeder(db, store, unconfigured, Microsoft.Extensions.Logging.Abstractions.NullLogger<DemoImageSeeder>.Instance).SeedAsync();
        store.Uploaded.Should().BeEmpty();

        var configured = Microsoft.Extensions.Options.Options.Create(new Ottoman.Application.Images.ImageOptions { CloudName = "c", ApiKey = "k", ApiSecret = "s" });
        var broken = new BrokenStore();
        await FluentActions.Awaiting(() => new DemoImageSeeder(db, broken, configured, Microsoft.Extensions.Logging.Abstractions.NullLogger<DemoImageSeeder>.Instance).SeedAsync())
            .Should().NotThrowAsync();
    }

    /// <summary>Bitwise CRC-32 (the PNG one), written differently from the code under test on purpose.</summary>
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    private sealed class BrokenStore : Ottoman.Application.Images.IImageStore
    {
        public Task<string> UploadAsync(Stream content, string folder, CancellationToken ct) => throw new HttpRequestException("no network");

        public Task DeleteAsync(string key, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task The_demo_accounts_documented_in_the_readme_can_all_sign_in()
    {
        foreach (var email in new[] { "kerim@ottoman.local", "tarik@ottoman.local", "klijent@ottoman.local", "elmir@ottoman.local" })
            (await api.CreateClient().LoginOk(email, ApiFixture.DemoPassword)).User.Email.Should().Be(email);
        // The credentials template of the rules (user name, password "test"); Adin is the "employee" account.
        foreach (var login in new[] { "desktop", "mobile", "employee", "adin@ottoman.local" })
            (await api.CreateClient().LoginOk(login, "test")).User.Should().NotBeNull();
    }
}
