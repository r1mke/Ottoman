using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Auth;
using Ottoman.Application.Catalog;
using Ottoman.Application.Clients;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Application.Reservations;
using Ottoman.Application.Scheduling;
using Ottoman.Application.Settings;
using Ottoman.Infrastructure.Auth;
using Ottoman.Infrastructure.Catalog;
using Ottoman.Infrastructure.Employees;
using Ottoman.Infrastructure.Identity;
using Ottoman.Infrastructure.Outbox;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Reservations;
using Ottoman.Infrastructure.Scheduling;
using Ottoman.Infrastructure.Seeding;
using Ottoman.Infrastructure.Settings;

namespace Ottoman.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<OttomanDbContext>(o => o.UseSqlServer(cs, sql => sql.EnableRetryOnFailure()));

        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireDigit = true;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireNonAlphanumeric = true;
                o.User.RequireUniqueEmail = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole>()
            .AddEntityFrameworkStores<OttomanDbContext>()
            .AddTokenProvider<DataProtectorTokenProvider<AppUser>>(TokenOptions.DefaultProvider);
        services.AddDataProtection().SetApplicationName("Ottoman")
            .PersistKeysToDbContext<OttomanDbContext>();

        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOutbox, DbOutbox>();
        services.AddScoped<IClientQueries, ClientQueries>();
        services.AddScoped<IServiceCatalog, ServiceCatalog>();
        services.AddScoped<IEmployeeService, EmployeeManagement>();
        services.AddScoped<AvailabilityLoader>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<ISalonSettingsService, SalonSettingsService>();

        services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));
        services.AddScoped<DatabaseSeeder>();
        return services;
    }
}
