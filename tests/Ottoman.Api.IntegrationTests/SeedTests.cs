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

        counts.Services.Should().BeGreaterThanOrEqualTo(5);
        counts.Products.Should().BeGreaterThanOrEqualTo(300);
        counts.Employees.Should().BeGreaterThanOrEqualTo(3);
        counts.Brands.Should().BeGreaterThanOrEqualTo(18);
        counts.Categories.Should().BeGreaterThanOrEqualTo(7);
        counts.Clients.Should().BeGreaterThanOrEqualTo(9);
        counts.Reservations.Should().BeGreaterThanOrEqualTo(150);
        counts.Orders.Should().BeGreaterThanOrEqualTo(40);
        counts.Reviews.Should().BeGreaterThanOrEqualTo(30);
        counts.Announcements.Should().BeGreaterThanOrEqualTo(4);
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
    public void The_embedded_catalog_is_consistent_with_itself()
    {
        var c = SeedCatalog.Load();
        var products = c.Products.Select(p => p.Id).ToHashSet();
        var categories = c.Categories.Select(x => x.Id).ToHashSet();
        var brands = c.Brands.Select(x => x.Id).ToHashSet();
        var haircuts = c.HaircutCategories.Select(x => x.Id).ToHashSet();

        c.Products.Count.Should().BeGreaterThan(300);
        c.Products.Select(p => p.Name.ToLowerInvariant()).Should().OnlyHaveUniqueItems();
        c.Products.Should().OnlyContain(p => (p.CategoryId == null || categories.Contains(p.CategoryId.Value)) && (p.BrandId == null || brands.Contains(p.BrandId.Value)));
        c.Products.Should().OnlyContain(p => p.SalePrice == null || p.SalePrice < p.Price);
        c.ProductImages.Should().OnlyContain(i => products.Contains(i.ProductId) && i.FileKey.StartsWith("ottoman/"));
        c.Gallery.Should().OnlyContain(g => haircuts.Contains(g.CategoryId) && g.ImageKey.StartsWith("ottoman/"));
        c.Gallery.Where(g => g.ContentHash != null).Select(g => g.ContentHash).Should().OnlyHaveUniqueItems();
        c.Services.Select(x => x.Name).Should().OnlyHaveUniqueItems();
        c.ShopSectionEntries.Should().OnlyContain(e => c.ShopSections.Any(s => s.Id == e.ShopSectionId));
        c.ShopSections.Select(s => s.Kind).Should().Contain(["OnSale", "BrandTiles", "CategoryTiles", "Recommended"]);
    }

    [Fact]
    public async Task The_real_catalog_is_loaded_with_picture_keys_and_nothing_is_uploaded()
    {
        var c = SeedCatalog.Load();
        var catalogNames = c.Products.Select(x => x.Name).ToList();
        var seen = await Query(async db => new
        {
            Products = await db.Products.CountAsync(),
            ActiveWithoutPicture = await db.Products.CountAsync(p => p.IsActive && !db.ProductImages.Any(i => i.ProductId == p.Id)
                && catalogNames.Contains(p.Name)),
            OnSale = await db.Products.CountAsync(p => p.SalePrice != null),
            LowStock = await db.Products.CountAsync(p => p.LowStockThreshold != null && p.Stock <= p.LowStockThreshold),
            Keys = await db.ProductImages.Select(i => i.FileKey).Take(50).ToListAsync(),
            BrandsWithLogo = await db.Brands.CountAsync(b => b.LogoKey != null),
            ServicesWithoutPicture = await db.Services.CountAsync(s => s.ImageKey == null),
            Gallery = await db.GalleryItems.CountAsync(),
            Haircuts = await db.HaircutCategories.CountAsync(),
            Sections = await db.ShopSections.Select(s => s.Kind).ToListAsync(),
            Entries = await db.Set<ShopSectionEntry>().CountAsync(),
            SalonDescription = await db.SalonSettings.Select(s => s.Description).SingleAsync(),
            StaffPhotos = await db.EmployeeProfiles.CountAsync(e => e.PhotoKey != null),
        });

        seen.Products.Should().BeGreaterThanOrEqualTo(c.Products.Count);
        seen.ActiveWithoutPicture.Should().Be(0);
        seen.OnSale.Should().BeGreaterThan(0);
        seen.LowStock.Should().BeGreaterThan(0);
        seen.Keys.Should().OnlyContain(k => k.StartsWith("ottoman/products/"), "the keys of the author's Cloudinary account are stored as they are");
        seen.BrandsWithLogo.Should().BeGreaterThan(10);
        seen.ServicesWithoutPicture.Should().Be(0);
        seen.Gallery.Should().BeGreaterThanOrEqualTo(c.Gallery.Count);
        seen.Haircuts.Should().BeGreaterThanOrEqualTo(6);
        seen.Sections.Should().Contain([ShopSectionKind.OnSale, ShopSectionKind.BrandTiles, ShopSectionKind.CategoryTiles, ShopSectionKind.Recommended]);
        seen.Entries.Should().BeGreaterThan(0);
        seen.SalonDescription.Should().NotBeNullOrWhiteSpace();
        seen.StaffPhotos.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Brand_tiles_of_the_front_page_point_at_brands_that_exist()
    {
        var dangling = await Query(db => (
            from e in db.Set<ShopSectionEntry>()
            join s in db.ShopSections on e.ShopSectionId equals s.Id
            where s.Kind == ShopSectionKind.BrandTiles && !db.Brands.Any(b => b.Id == e.EntityId)
            select e).CountAsync());

        dangling.Should().Be(0, "ids of the file are translated to the ids of this database");
    }

    [Fact]
    public async Task The_clients_have_varied_reviews_replies_and_one_the_salon_took_down()
    {
        var reviews = await Query(db => db.Reviews.AsNoTracking().ToListAsync());

        reviews.Select(r => r.Rating).Distinct().Count().Should().BeGreaterThanOrEqualTo(4, "ratings are not all the same");
        reviews.Select(r => r.TargetType).Distinct().Should().BeEquivalentTo([ReviewTargetType.Employee, ReviewTargetType.Service, ReviewTargetType.Product]);
        reviews.Should().Contain(r => r.Comment != null);
        reviews.Should().Contain(r => r.ReplyText != null);
        reviews.Count(r => r.IsHidden).Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Seeding_the_whole_demo_again_adds_nothing_anywhere()
    {
        async Task<int[]> Counts() => await Query(async db => new[]
        {
            await db.Users.CountAsync(), await db.Products.CountAsync(), await db.ProductImages.CountAsync(),
            await db.Brands.CountAsync(), await db.Categories.CountAsync(), await db.Services.CountAsync(),
            await db.GalleryItems.CountAsync(), await db.HaircutCategories.CountAsync(), await db.EmployeeTitles.CountAsync(),
            await db.ShopSections.CountAsync(), await db.Set<ShopSectionEntry>().CountAsync(), await db.Announcements.CountAsync(),
            await db.UserNotifications.CountAsync(), await db.Reservations.CountAsync(), await db.Orders.CountAsync(),
            await db.Payments.CountAsync(), await db.Reviews.CountAsync(), await db.EmployeeProfiles.CountAsync(),
            await db.EmployeeServices.CountAsync(),
        });
        var before = await Counts();

        for (var run = 0; run < 2; run++)
        {
            await using var scope = api.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        }

        (await Counts()).Should().Equal(before);
    }

    [Fact]
    public async Task A_catalog_that_an_admin_has_edited_is_not_overwritten()
    {
        string name;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var product = await db.Products.OrderByDescending(p => p.Id).FirstAsync();
            name = product.Name;
            product.Price = 77.77m;
            product.Stock = 3;
            await db.SaveChangesAsync();
        }

        await using (var scope = api.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();

        var after = await Query(db => db.Products.AsNoTracking().SingleAsync(p => p.Name == name));
        after.Price.Should().Be(77.77m);
        after.Stock.Should().Be(3);
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
