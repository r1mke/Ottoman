using Microsoft.EntityFrameworkCore;
using Ottoman.Infrastructure;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Seeding;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddHealthChecks()
        .AddSqlServer(
            builder.Configuration.GetConnectionString("Default")!,
            name: "sql",
            tags: ["ready"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });

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
