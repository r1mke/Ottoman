using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Audit;
using Ottoman.Application.Auth;
using Ottoman.Application.Catalog;
using Ottoman.Application.Clients;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Application.Images;
using Ottoman.Application.Notifications;
using Ottoman.Application.Orders;
using Ottoman.Application.Payments;
using Ottoman.Application.Push;
using Ottoman.Application.Recommendations;
using Ottoman.Application.Reports;
using Ottoman.Application.Reservations;
using Ottoman.Application.Reviews;
using Ottoman.Application.Scheduling;
using Ottoman.Application.Settings;
using Ottoman.Application.Shop;
using Ottoman.Infrastructure.Auth;
using Ottoman.Infrastructure.Catalog;
using Ottoman.Infrastructure.Employees;
using Ottoman.Infrastructure.Identity;
using Ottoman.Infrastructure.Images;
using Ottoman.Infrastructure.Notifications;
using Ottoman.Infrastructure.Orders;
using Ottoman.Infrastructure.Outbox;
using Ottoman.Infrastructure.Payments;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Push;
using Ottoman.Infrastructure.Recommendations;
using Ottoman.Infrastructure.Reports;
using Ottoman.Infrastructure.Reservations;
using Ottoman.Infrastructure.Reviews;
using Ottoman.Infrastructure.Scheduling;
using Ottoman.Infrastructure.Security;
using Ottoman.Infrastructure.Seeding;
using Ottoman.Infrastructure.Settings;
using Ottoman.Infrastructure.Shop;

namespace Ottoman.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Set to false (tests) to keep the API from publishing the outbox to RabbitMQ.</summary>
    public const string PublishOutboxSetting = "Messaging:PublishOutbox";

    /// <summary>
    /// The API's half of messaging: it publishes the outbox to RabbitMQ itself (the worker only consumes).
    /// </summary>
    public static IServiceCollection AddOutboxPublishing(this IServiceCollection services, IConfiguration config)
    {
        if (!string.Equals(config[PublishOutboxSetting], "false", StringComparison.OrdinalIgnoreCase))
            services.AddHostedService<Messaging.OutboxDispatcher>();
        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        // One RabbitMQ connection per process (API: publisher, worker: consumer), opened on first use.
        services.Configure<Messaging.RabbitOptions>(config.GetSection(Messaging.RabbitOptions.Section));
        services.AddSingleton<Messaging.RabbitConnection>();

        services.AddSingleton<RecommendationCache>();
        services.AddSingleton<IRecommendationCache>(sp => sp.GetRequiredService<RecommendationCache>());
        services.AddSingleton<CatalogChangeInterceptor>();
        services.AddDbContext<OttomanDbContext>((sp, o) => o
            .UseSqlServer(cs, sql => sql.EnableRetryOnFailure())
            .AddInterceptors(sp.GetRequiredService<CatalogChangeInterceptor>()));

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
        services.AddScoped<StoredKeyEncryption>();
        services.AddSingleton<IResetTokenVault, ResetTokenVault>();
        services.AddDataProtection().SetApplicationName("Ottoman")
            .PersistKeysToDbContext<OttomanDbContext>()
            .ProtectKeysWithConfiguredSecret(config.GetSection(KeyEncryptionOptions.Section).Get<KeyEncryptionOptions>() ?? new());

        services.AddOptions<JwtOptions>()
            .Bind(config.GetSection(JwtOptions.Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOutbox, DbOutbox>();
        services.AddMemoryCache();
        services.AddSingleton<IAccessGate, UserAccessGate>();
        services.AddScoped<IClientQueries, ClientQueries>();
        services.AddScoped<IServiceCatalog, ServiceCatalog>();
        services.AddScoped<IEmployeeService, EmployeeManagement>();
        services.AddScoped<IEmployeeTitles, Employees.EmployeeTitles>();
        services.AddScoped<AvailabilityLoader>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<ISalonSettingsService, SalonSettingsService>();
        services.AddScoped<Ottoman.Application.Salon.ISalonProfileService, Salon.SalonProfileService>();
        services.AddScoped<Ottoman.Application.Gallery.IGalleryService, Gallery.GalleryService>();

        services.AddOptions<PaymentOptions>().Bind(config.GetSection(PaymentOptions.Section));
        services.AddScoped<IProductCatalog, ProductCatalog>();
        services.AddScoped<Ottoman.Application.Shop.IShopSectionService, Shop.ShopSectionService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPaymentWebhookHandler, PaymentWebhookHandler>();
        services.AddScoped<PendingRefunds>();
        services.AddScoped<PaymentFinalizer>();
        if (string.IsNullOrWhiteSpace(config[$"{PaymentOptions.Section}:SecretKey"]))
        {
            // No processor configured: nothing is ever charged, and orders only become paid via a signed webhook.
            services.AddSingleton<OfflinePaymentGateway>();
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<OfflinePaymentGateway>());
        }
        else
        {
            services.AddHttpClient<IPaymentGateway, StripeGateway>(c => c.BaseAddress = StripeGateway.BaseAddress);
        }
        services.Configure<ImageOptions>(config.GetSection(ImageOptions.Section));
        services.AddSingleton<ImageUrlBuilder>();
        services.AddScoped<IImageAttachments, ImageAttachments>();
        if (config.GetSection(ImageOptions.Section).Get<ImageOptions>() is { Configured: true })
        {
            services.AddHttpClient<IImageStore, CloudinaryImageStore>(c =>
            {
                c.BaseAddress = CloudinaryImageStore.BaseAddress;
                c.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            // No Cloudinary account: pictures are accepted and validated but go nowhere.
            services.AddSingleton<OfflineImageStore>();
            services.AddSingleton<IImageStore>(sp => sp.GetRequiredService<OfflineImageStore>());
        }
        services.Configure<PushOptions>(config.GetSection(PushOptions.Section));
        services.AddScoped<IDeviceRegistry, DeviceRegistry>();
        services.AddSingleton<IAuditLog, Ottoman.Infrastructure.Audit.AuditLog>();
        services.AddSingleton<Ottoman.Application.Idempotency.IIdempotencyStore, Ottoman.Infrastructure.Idempotency.IdempotencyStore>();
        services.AddScoped<IAuditQueries, Ottoman.Infrastructure.Audit.AuditQueries>();
        if (config.GetSection(PushOptions.Section).Get<PushOptions>() is { Configured: true })
        {
            services.AddHttpClient(FcmPushSender.ClientName, c => c.Timeout = TimeSpan.FromSeconds(15));
            services.AddSingleton<IPushSender, FcmPushSender>();
        }
        else
        {
            // No Firebase key: pushes are recorded in memory, nothing leaves the machine.
            services.AddSingleton<OfflinePushSender>();
            services.AddSingleton<IPushSender>(sp => sp.GetRequiredService<OfflinePushSender>());
        }
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddSingleton<IReportExporter, ReportExporter>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<ReminderService>();
        services.AddScoped<Ottoman.Infrastructure.Outbox.OutboxCleanup>();

        services.Configure<SeedOptions>(config.GetSection(SeedOptions.Section));
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<DemoImageSeeder>();
        return services;
    }
}
