using Ottoman.Application.Notifications;
using Ottoman.Infrastructure;
using Ottoman.Infrastructure.Orders;
using Ottoman.Worker;
using Ottoman.Worker.Email;
using Ottoman.Worker.Messaging;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog((_, cfg) => cfg.ReadFrom.Configuration(builder.Configuration).WriteTo.Console());

// Same database, payment gateway and domain services as the API: the worker is the other half of the system.
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection(RabbitOptions.Section));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.AddSingleton<RabbitConnection>();
builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();
// Needs a mail sender, so it lives here and not in the shared infrastructure registration.
builder.Services.AddScoped<Ottoman.Infrastructure.Notifications.EventProcessor>();

builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<NotificationConsumer>();
builder.Services.AddHostedService<ReminderScheduler>();
builder.Services.AddHostedService<OutboxCleanupScheduler>();
builder.Services.AddHostedService<OrderExpiryService>();
builder.Services.AddHostedService<HeartbeatService>();

await builder.Build().RunAsync();
