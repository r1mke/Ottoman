using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Identity;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Seeding;

/// <summary>Idempotent: safe to run on every start.</summary>
public partial class DatabaseSeeder(
    OttomanDbContext db,
    UserManager<AppUser> users,
    RoleManager<IdentityRole> roles,
    IOptions<SeedOptions> options,
    ILogger<DatabaseSeeder> log)
{
    private readonly SeedOptions _opt = options.Value;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        if (!await db.SalonSettings.AnyAsync(ct))
        {
            db.SalonSettings.Add(new SalonSettings { Id = 1, Name = "Ottoman" });
            await db.SaveChangesAsync(ct);
        }

        await SeedSalonBasicsAsync(ct);

        if (!string.IsNullOrWhiteSpace(_opt.AdminEmail) && !string.IsNullOrWhiteSpace(_opt.AdminPassword))
            await EnsureUserAsync(_opt.AdminEmail, _opt.AdminPassword, "Admin", "Ottoman", Roles.Admin);

        if (_opt.DemoData) await SeedDemoAsync(ct);
    }

    /// <summary>Opening hours and the haircut styles the gallery starts with; only filled in when there are none.</summary>
    private async Task SeedSalonBasicsAsync(CancellationToken ct)
    {
        if (!await db.SalonOpeningHours.AnyAsync(ct))
        {
            for (var d = 0; d < 7; d++)
            {
                var day = (DayOfWeek)d;
                db.SalonOpeningHours.Add(day switch
                {
                    DayOfWeek.Sunday => new SalonOpeningHours { Day = day },
                    DayOfWeek.Saturday => new SalonOpeningHours { Day = day, OpenMinutes = 9 * 60, CloseMinutes = 15 * 60 },
                    _ => new SalonOpeningHours { Day = day, OpenMinutes = 9 * 60, CloseMinutes = 19 * 60 },
                });
            }
        }

        if (!await db.HaircutCategories.AnyAsync(ct))
        {
            var names = new[] { "Buzz cut", "French crop", "Fade", "Klasično šišanje", "Duga kosa", "Brada" };
            for (var i = 0; i < names.Length; i++)
                db.HaircutCategories.Add(new HaircutCategory { Name = names[i], SortOrder = i });
        }

        // The shop front page starts with two rows the admin can then rearrange, edit or delete.
        if (!await db.ShopSections.AnyAsync(ct))
        {
            db.ShopSections.Add(new ShopSection { Title = "Novo u ponudi", Kind = ShopSectionKind.NewArrivals, SortOrder = 0 });
            db.ShopSections.Add(new ShopSection { Title = "Akcije", Kind = ShopSectionKind.OnSale, SortOrder = 1 });
        }

        // The category tiles, the brand tiles and "you may like" are blocks like any section: the admin can
        // reorder, hide or retitle them. A shop that has none yet gets them, ahead of the existing sections.
        var first = await db.ShopSections.MinAsync(s => (int?)s.SortOrder, ct) ?? 3;
        async Task EnsureBlock(ShopSectionKind kind, string title, string eyebrow, int maxItems, int order)
        {
            if (await db.ShopSections.AnyAsync(s => s.Kind == kind, ct)) return;
            db.ShopSections.Add(new ShopSection { Title = title, Eyebrow = eyebrow, Kind = kind, MaxItems = maxItems, SortOrder = order });
        }
        await EnsureBlock(ShopSectionKind.CategoryTiles, "Kategorije", "Asortiman", 6, first - 3);
        await EnsureBlock(ShopSectionKind.BrandTiles, "Brendovi", "Marke", 12, first - 2);
        await EnsureBlock(ShopSectionKind.Recommended, "Možda vam se sviđa", "Za vas", 10, first - 1);

        // A salon profile nobody has touched yet gets a short demo text.
        var settings = await db.SalonSettings.SingleAsync(ct);
        if (_opt.DemoData && settings.Description is null && settings.Address is null && settings.MapsUrl is null)
        {
            settings.Description = "Ottoman je moderan frizerski salon u kojem se tradicija brijanja spaja s današnjim stilom. "
                + "Naši frizeri brinu o svakom detalju, od klasičnog šišanja do uređivanja brade.";
            settings.Address = "Ferhadija 12, 71000 Sarajevo";
            settings.Phone = "033 123 456";
            settings.MapsUrl = "https://www.google.com/maps/search/?api=1&query=43.8586,18.4310";
            settings.Latitude = 43.8586;
            settings.Longitude = 18.4310;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<AppUser> EnsureUserAsync(
        string email, string password, string first, string last, string role, DateTime? registeredAtUtc = null)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;

        user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = first,
            LastName = last,
            RegisteredAtUtc = registeredAtUtc ?? DateTime.UtcNow,
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            throw new InvalidOperationException(
                $"Seeding user {email} failed: {string.Join("; ", created.Errors.Select(e => e.Description))}");
        await users.AddToRoleAsync(user, role);
        log.LogInformation("Seeded {Role} {Email}", role, email);
        return user;
    }

    private async Task SeedDemoAsync(CancellationToken ct)
    {
        if (await db.Services.AnyAsync(ct)) return;
        var pw = _opt.DemoPassword;

        var services = new[]
        {
            new Service { Name = "Šišanje", Description = "Klasično muško šišanje", Price = 25m, DurationMinutes = 30 },
            new Service { Name = "Šišanje & brada", Description = "Šišanje uz uređivanje brade", Price = 40m, DurationMinutes = 60 },
            new Service { Name = "Uređivanje brade", Description = "Oblikovanje i njega brade", Price = 15m, DurationMinutes = 30 },
            new Service { Name = "Srednja / duga kosa", Description = "Šišanje srednje i duge kose", Price = 35m, DurationMinutes = 45 },
            new Service { Name = "Masaža glave", Description = "Opuštajuća masaža glave", Price = 10m, DurationMinutes = 15 },
            new Service { Name = "Brijanje vrućim ručnikom", Description = "Tradicionalno brijanje britvom i toplim ručnikom", Price = 20m, DurationMinutes = 30 },
            new Service { Name = "Dječije šišanje", Description = "Šišanje za dječake do 12 godina", Price = 15m, DurationMinutes = 30 },
            new Service { Name = "Bojenje brade", Description = "Prirodno bojenje sijedih dlaka u bradi", Price = 20m, DurationMinutes = 30 },
        };
        db.Services.AddRange(services);
        await db.SaveChangesAsync(ct);

        var kerim = await EnsureUserAsync("kerim@ottoman.local", pw, "Kerim", "Begić", Roles.Employee);
        var tarik = await EnsureUserAsync("tarik@ottoman.local", pw, "Tarik", "Mujkić", Roles.Employee);
        var adin = await EnsureUserAsync("adin@ottoman.local", pw, "Adin", "Hasanović", Roles.Employee);
        var demoClient = await EnsureUserAsync("klijent@ottoman.local", pw, "Demo", "Klijent", Roles.Client);

        AddEmployee(kerim, "Master Barber", "Majstor u usponu", services);
        AddEmployee(tarik, "Junior Barber", null, services.Take(3).ToArray());
        AddEmployee(adin, "Barber", "Specijalista za fade i klasična brijanja", services.Where(s => s.Name != "Masaža glave").ToArray());

        var dandy = new Brand { Name = "Dandy" };
        var proraso = new Brand { Name = "Proraso" };
        var reuzel = new Brand { Name = "Reuzel" };
        var wax = new Category { Name = "Voskovi", Description = "Voskovi i pomade za stilizovanje" };
        var beard = new Category { Name = "Njega brade", Description = "Ulja i balzami za bradu" };
        var shampoo = new Category { Name = "Šamponi", Description = "Šamponi i njega kose" };
        var aftershave = new Category { Name = "Poslije brijanja", Description = "Losioni za lice poslije brijanja" };
        var tools = new Category { Name = "Pribor", Description = "Češljevi i četke" };
        var products = new[]
        {
            new Product { Name = "Vosak za kosu Dandy Shaping Pomade", Description = "Pomada srednje fiksacije, prirodan efekt.", Brand = dandy, Category = wax, Price = 10m, Stock = 25, LowStockThreshold = 5 },
            new Product { Name = "Dandy Matt Clay", Description = "Mat glina jake fiksacije.", Brand = dandy, Category = wax, Price = 12m, Stock = 18, LowStockThreshold = 5 },
            new Product { Name = "Dandy Beard Oil", Description = "Ulje za njegu i omekšavanje brade.", Brand = dandy, Category = beard, Price = 14m, Stock = 12, LowStockThreshold = 4 },
            new Product { Name = "Proraso Beard Balm", Description = "Balzam za bradu s eukaliptusom.", Brand = proraso, Category = beard, Price = 16m, Stock = 3, LowStockThreshold = 5 },
            new Product { Name = "Reuzel Daily Shampoo", Description = "Svakodnevni šampon blage formule.", Brand = reuzel, Category = shampoo, Price = 13m, Stock = 20, LowStockThreshold = 5 },
            new Product { Name = "Reuzel Tea Tree Shampoo", Description = "Šampon s čajevcem protiv perutanja.", Brand = reuzel, Category = shampoo, Price = 15m, Stock = 14, LowStockThreshold = 4 },
            new Product { Name = "Proraso After Shave Lotion", Description = "Osvježavajući losion poslije brijanja.", Brand = proraso, Category = aftershave, Price = 11m, Stock = 16, LowStockThreshold = 4 },
            new Product { Name = "Dandy After Shave Balm", Description = "Umirujući balzam za lice poslije brijanja.", Brand = dandy, Category = aftershave, Price = 12m, Stock = 9, LowStockThreshold = 4 },
            new Product { Name = "Drveni češalj", Description = "Ručno rađen češalj od bukovine.", Category = tools, Price = 6m, Stock = 30, LowStockThreshold = 6 },
            new Product { Name = "Četka za bradu", Description = "Četka od prirodne čekinje.", Category = tools, Price = 9m, Stock = 22, LowStockThreshold = 5 },
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync(ct);

        // Clients registered over the last four months, so "new clients" in the reports has a shape.
        var clients = new List<AppUser> { demoClient };
        for (var i = 0; i < DemoClients.Length; i++)
        {
            var (first, last, email) = DemoClients[i];
            clients.Add(await EnsureUserAsync(email, pw, first, last, Roles.Client, DateTime.UtcNow.AddDays(-110 + i * 13)));
        }

        await SeedActivityAsync([kerim, tarik, adin], clients, services, products, ct);
        log.LogInformation("Seeded demo data");
    }

    private void AddEmployee(AppUser user, string title, string? bio, IEnumerable<Service> assigned)
    {
        // Titles are a list of their own; a barber points at one (it is created here if the list does not have it yet).
        var titleRow = db.EmployeeTitles.Local.FirstOrDefault(t => string.Equals(t.Name, title, StringComparison.OrdinalIgnoreCase))
                       ?? db.EmployeeTitles.FirstOrDefault(t => t.Name == title)
                       ?? new EmployeeTitle { Name = title };
        var profile = new EmployeeProfile { UserId = user.Id, Title = titleRow, Bio = bio, Gender = Gender.Male };
        profile.Services = assigned.Select(s => new EmployeeService { EmployeeId = user.Id, ServiceId = s.Id }).ToList();

        // Mon-Fri 09:00-17:00 with a lunch break, Sat 09:00-14:00.
        DayOfWeek[] weekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
        foreach (var d in weekdays)
            profile.WorkingHours.Add(new WorkingHours
            {
                EmployeeId = user.Id,
                DayOfWeek = d,
                Start = new TimeOnly(9, 0),
                End = new TimeOnly(17, 0),
                BreakStart = new TimeOnly(12, 0),
                BreakEnd = new TimeOnly(12, 30),
            });
        profile.WorkingHours.Add(new WorkingHours
        {
            EmployeeId = user.Id,
            DayOfWeek = DayOfWeek.Saturday,
            Start = new TimeOnly(9, 0),
            End = new TimeOnly(14, 0),
        });
        db.EmployeeProfiles.Add(profile);
    }
}
