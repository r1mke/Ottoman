using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application;
using Ottoman.Application.Common;
using Ottoman.Infrastructure;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Seeding;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // A production instance must not start with example secrets or demo accounts.
    var problems = StartupGuards.Problems(
        builder.Environment.IsDevelopment(),
        builder.Configuration["Jwt:SigningKey"],
        builder.Configuration.GetValue<bool>("Seed:DemoData"),
        builder.Configuration["Seed:AdminPassword"],
        builder.Configuration["DataProtection:KeyEncryptionSecret"],
        builder.Configuration.GetSection("Cors:Origins").Get<string[]>());
    if (problems.Count > 0)
        throw new InvalidOperationException(
            "Unsafe configuration:" + Environment.NewLine + " - " + string.Join(Environment.NewLine + " - ", problems));

    // A small default cap blunts cheap memory abuse; the picture upload endpoints raise it for themselves
    // ([RequestSizeLimit] + [RejectOversizedUpload]).
    builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1_000_000);

    // Behind a reverse proxy every request would otherwise look like it comes from the proxy, which would make
    // the per-IP rate limits useless. Only proxies listed in config are trusted to set X-Forwarded-For.
    var trustedProxies = builder.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [];
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        foreach (var p in trustedProxies) o.KnownProxies.Add(System.Net.IPAddress.Parse(p));
    });

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddOutboxPublishing(builder.Configuration);
    builder.Services.AddApplication();
    builder.Services.AddOttomanAuth(builder.Configuration);
    builder.Services.AddControllers(o =>
    {
        o.Filters.Add<TextSafetyFilter>();
        o.Filters.Add<ValidationFilter>();
        o.Filters.Add<AuditFilter>();
    }).ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = InvalidModelState.Respond);
    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<HttpExceptionHandler>();
    builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Browsers are not a client of this API (the apps are native), so CORS is off unless exact origins are listed.
    var corsOrigins = CorsRules.Normalize(
        builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [], builder.Environment.IsDevelopment());
    if (corsOrigins.Count > 0)
        builder.Services.AddCors(o => o.AddPolicy("web", p => p
            .WithOrigins([.. corsOrigins])
            .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key")
            .WithExposedHeaders("Idempotent-Replayed")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("Default")!,
            name: "sql",
            tags: ["ready"]);

    var app = builder.Build();

    if (trustedProxies.Length > 0) app.UseForwardedHeaders();
    if (!app.Environment.IsDevelopment()) app.UseHsts();

    app.Use(async (ctx, next) =>
    {
        // A JSON API: nothing here should be sniffed as HTML, framed, cached with credentials, or leak the URL on navigation.
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        ctx.Response.Headers["X-Frame-Options"] = "DENY";
        ctx.Response.Headers["Cache-Control"] = "no-store";
        await next();
    });

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        await scope.ServiceProvider.GetRequiredService<Ottoman.Infrastructure.Security.StoredKeyEncryption>().EnsureEncryptedAsync(default);
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference().AllowAnonymous();
    }

    if (corsOrigins.Count > 0) app.UseCors("web");
    app.UseAuthentication();
    app.UseAuthorization();
    // After authentication, so the general write limit can count per signed-in user instead of per address.
    app.UseRateLimiter();

    app.MapControllers();
    app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Ottoman.Api terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
