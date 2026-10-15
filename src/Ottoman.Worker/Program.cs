using Ottoman.Worker;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog((_, cfg) => cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());

// Phase 0 skeleton. Outbox dispatcher, email/in-app consumers and the reminder
// scheduler are added in Phase 5 (see docs/04-plan-implementacije.md).
builder.Services.AddHostedService<HeartbeatService>();

await builder.Build().RunAsync();
