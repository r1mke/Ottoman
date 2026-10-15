using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Infrastructure.Identity;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Seeding;

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
            .AddEntityFrameworkStores<OttomanDbContext>();

        services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));
        services.AddScoped<DatabaseSeeder>();
        return services;
    }
}
