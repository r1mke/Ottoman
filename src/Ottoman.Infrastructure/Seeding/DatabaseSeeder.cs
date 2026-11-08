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
    CatalogSeeder catalog,
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

        // The demo catalog comes first: it fills the shop front page, gallery styles and salon profile, so the
        // defaults below only fill what is still missing.
        if (_opt.DemoData) await catalog.SeedAsync(ct);

        await SeedSalonBasicsAsync(ct);

        if (!string.IsNullOrWhiteSpace(_opt.AdminEmail) && !string.IsNullOrWhiteSpace(_opt.AdminPassword))
            await EnsureUserAsync(_opt.AdminEmail, _opt.AdminPassword, "Admin", "Ottoman", Roles.Admin);

        if (_opt.DemoData)
        {
            await SeedDemoAsync(ct);
            await SeedTemplateAccountsAsync(ct);
        }
    }

    /// <summary>The password the faculty's credentials template prescribes for the demo accounts.</summary>
    public const string TemplatePassword = "test";

    /// <summary>
    /// The accounts of the credentials template in the README: "desktop" (administrator, desktop app), "mobile"
    /// (client, mobile app) and "employee" (the demo barber Adin, both apps): the user name is the name of the role, as
    /// the README template of the rules asks. All have the password "test". That
    /// password is far below the policy, so it is hashed directly here instead of going through the validators; this
    /// only ever runs with demo data, which the production start-up guards refuse. Registration and password changes
    /// keep the strong policy. Idempotent: an existing account is left as it is.
    /// </summary>
    private async Task SeedTemplateAccountsAsync(CancellationToken ct)
    {
        await EnsureTemplateUserAsync("desktop", "desktop@ottoman.local", "Desktop", "Administrator", Roles.Admin);
        await EnsureTemplateUserAsync("mobile", "mobile@ottoman.local", "Mobile", "Klijent", Roles.Client);

        var adin = await users.FindByEmailAsync("adin@ottoman.local");
        if (adin is not null && await users.FindByNameAsync("employee") is null)
        {
            // Also renames the account an earlier version of the seed called "uposlenik".
            adin.UserName = "employee";
            adin.PasswordHash = users.PasswordHasher.HashPassword(adin, TemplatePassword);
            await users.UpdateNormalizedUserNameAsync(adin);
            await users.UpdateSecurityStampAsync(adin);
            await users.UpdateAsync(adin);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureTemplateUserAsync(string userName, string email, string first, string last, string role)
    {
        if (await users.FindByNameAsync(userName) is not null) return;
        var user = new AppUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FirstName = first,
            LastName = last,
            RegisteredAtUtc = DateTime.UtcNow,
        };
        user.PasswordHash = users.PasswordHasher.HashPassword(user, TemplatePassword);
        var created = await users.CreateAsync(user); // no password argument: the hash above is kept as it is
        if (!created.Succeeded)
            throw new InvalidOperationException(
                $"Seeding user {userName} failed: {string.Join("; ", created.Errors.Select(e => e.Description))}");
        await users.AddToRoleAsync(user, role);
        log.LogInformation("Seeded template account {UserName} ({Role})", userName, role);
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

    /// <summary>The barbers, the clients and their history on top of the catalog. Skipped once the demo barbers exist.</summary>
    private async Task SeedDemoAsync(CancellationToken ct)
    {
        if (await users.FindByEmailAsync("kerim@ottoman.local") is not null) return;
        var pw = _opt.DemoPassword;

        var services = await db.Services.OrderBy(s => s.Id).ToListAsync(ct);
        var staffPhotos = SeedCatalog.Load().DemoStaffPhotos.ToDictionary(p => p.Email, p => p.PhotoKey);

        var kerim = await EnsureUserAsync("kerim@ottoman.local", pw, "Kerim", "Begić", Roles.Employee);
        var tarik = await EnsureUserAsync("tarik@ottoman.local", pw, "Tarik", "Mujkić", Roles.Employee);
        var adin = await EnsureUserAsync("adin@ottoman.local", pw, "Adin", "Hasanović", Roles.Employee);
        var demoClient = await EnsureUserAsync("klijent@ottoman.local", pw, "Demo", "Klijent", Roles.Client);

        AddEmployee(kerim, "Master Barber", "Majstor u usponu", services, staffPhotos.GetValueOrDefault("kerim@ottoman.local"));
        AddEmployee(tarik, "Junior Barber", null, services.Take(3).ToArray(), staffPhotos.GetValueOrDefault("tarik@ottoman.local"));
        AddEmployee(adin, "Barber", "Specijalista za fade i klasična brijanja", services.Where(s => s.Name != "Masaža glave").ToArray(), null);
        await db.SaveChangesAsync(ct);

        // Fictional clients registered over the last four months, so "new clients" in the reports has a shape.
        var clients = new List<AppUser> { demoClient };
        for (var i = 0; i < DemoClients.Length; i++)
        {
            var (first, last, email) = DemoClients[i];
            clients.Add(await EnsureUserAsync(email, pw, first, last, Roles.Client, DateTime.UtcNow.AddDays(-110 + i * 13)));
        }

        var products = await db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync(ct);
        await SeedActivityAsync([kerim, tarik, adin], clients, services, products, ct);
        log.LogInformation("Seeded demo data");
    }

    private void AddEmployee(AppUser user, string title, string? bio, IEnumerable<Service> assigned, string? photoKey)
    {
        // Titles are a list of their own; a barber points at one (it is created here if the list does not have it yet).
        var titleRow = db.EmployeeTitles.Local.FirstOrDefault(t => string.Equals(t.Name, title, StringComparison.OrdinalIgnoreCase))
                       ?? db.EmployeeTitles.FirstOrDefault(t => t.Name == title)
                       ?? new EmployeeTitle { Name = title };
        var profile = new EmployeeProfile { UserId = user.Id, Title = titleRow, Bio = bio, Gender = Gender.Male, PhotoKey = photoKey };
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
