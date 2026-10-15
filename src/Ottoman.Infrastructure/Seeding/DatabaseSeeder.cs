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
public class DatabaseSeeder(
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

        if (!string.IsNullOrWhiteSpace(_opt.AdminEmail) && !string.IsNullOrWhiteSpace(_opt.AdminPassword))
            await EnsureUserAsync(_opt.AdminEmail, _opt.AdminPassword, "Admin", "Ottoman", Roles.Admin);

        if (_opt.DemoData) await SeedDemoAsync(ct);
    }

    private async Task<AppUser> EnsureUserAsync(string email, string password, string first, string last, string role)
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
            RegisteredAtUtc = DateTime.UtcNow,
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
        };
        db.Services.AddRange(services);
        await db.SaveChangesAsync(ct);

        var kerim = await EnsureUserAsync("kerim@ottoman.local", pw, "Kerim", "Begić", Roles.Employee);
        var tarik = await EnsureUserAsync("tarik@ottoman.local", pw, "Tarik", "Mujkić", Roles.Employee);
        await EnsureUserAsync("klijent@ottoman.local", pw, "Demo", "Klijent", Roles.Client);

        AddEmployee(kerim, "Master Barber", "Majstor u usponu", services);
        AddEmployee(tarik, "Junior Barber", null, services.Take(3).ToArray());

        var dandy = new Brand { Name = "Dandy" };
        var proraso = new Brand { Name = "Proraso" };
        var wax = new Category { Name = "Voskovi", Description = "Voskovi i pomade za stilizovanje" };
        var beard = new Category { Name = "Njega brade", Description = "Ulja i balzami za bradu" };
        db.Products.AddRange(
            new Product { Name = "Vosak za kosu Dandy Shaping Pomade", Description = "Pomada srednje fiksacije, prirodan efekt.", Brand = dandy, Category = wax, Price = 10m, Stock = 25, LowStockThreshold = 5 },
            new Product { Name = "Dandy Matt Clay", Description = "Mat glina jake fiksacije.", Brand = dandy, Category = wax, Price = 12m, Stock = 18, LowStockThreshold = 5 },
            new Product { Name = "Dandy Beard Oil", Description = "Ulje za njegu i omekšavanje brade.", Brand = dandy, Category = beard, Price = 14m, Stock = 12, LowStockThreshold = 4 },
            new Product { Name = "Proraso Beard Balm", Description = "Balzam za bradu s eukaliptusom.", Brand = proraso, Category = beard, Price = 16m, Stock = 3, LowStockThreshold = 5 });
        await db.SaveChangesAsync(ct);
        log.LogInformation("Seeded demo data");
    }

    private void AddEmployee(AppUser user, string title, string? bio, IEnumerable<Service> assigned)
    {
        var profile = new EmployeeProfile { UserId = user.Id, Title = title, Bio = bio };
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
