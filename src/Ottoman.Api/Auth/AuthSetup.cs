using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ottoman.Application.Auth;
using Ottoman.Domain;
using Ottoman.Infrastructure.Auth;

namespace Ottoman.Api.Auth;

public static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string Staff = nameof(Staff);          // Admin or Employee
    public const string ClientOnly = nameof(ClientOnly);

    public const string LoginLimit = "login";
    public const string RegisterLimit = "register";
    public const string ForgotLimit = "forgot";
    public const string RefreshLimit = "refresh";
    public const string OrderLimit = "orders";
}

public class RateLimitOptions
{
    public const string Section = "RateLimiting";
    public bool Enabled { get; set; } = true;
    public int LoginPerMinute { get; set; } = 5;
    public int RegisterPerHour { get; set; } = 5;
    public int ForgotPerHour { get; set; } = 5;
    public int RefreshPerMinute { get; set; } = 30;
    public int OrdersPerHour { get; set; } = 30;

    /// <summary>Every signed-in user (or address, if anonymous) may change data this many times a minute.</summary>
    public int WritesPerMinute { get; set; } = 120;
}

public static class ClaimsExtensions
{
    /// <summary>Highest-privilege role wins if a token somehow carries several.</summary>
    public static Ottoman.Application.Common.Actor ToActor(this ClaimsPrincipal user)
    {
        var kind = user.IsInRole(Roles.Admin) ? Ottoman.Domain.Reservations.ActorKind.Admin
            : user.IsInRole(Roles.Employee) ? Ottoman.Domain.Reservations.ActorKind.Employee
            : Ottoman.Domain.Reservations.ActorKind.Client;
        return new Ottoman.Application.Common.Actor(user.UserId(), kind);
    }

    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? throw new InvalidOperationException("Authenticated principal has no 'sub' claim.");
}

public static class AuthSetup
{
    public static IServiceCollection AddOttomanAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured through IOptions so the signing key is read after all config sources are applied.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var o = jwt.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = o.Issuer,
                    ValidateAudience = true,
                    ValidAudience = o.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                };

                // A valid signature is not enough: a blocked or deleted account must stop working at once,
                // not when its 15-minute token runs out.
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?.FindFirstValue("sub");
                        var gate = context.HttpContext.RequestServices.GetRequiredService<IAccessGate>();
                        if (userId is null || !await gate.IsAllowedAsync(userId, context.HttpContext.RequestAborted))
                            context.Fail("The account is blocked or no longer exists.");
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.Staff, p => p.RequireRole(Roles.Admin, Roles.Employee))
            // Deny by default: an endpoint nobody remembered to protect still demands a signed-in user.
            // Public ones must say so with [AllowAnonymous].
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.ClientOnly, p => p.RequireRole(Roles.Client));

        services.Configure<RateLimitOptions>(config.GetSection(RateLimitOptions.Section));
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    title = "Previše pokušaja.",
                    detail = "Previše pokušaja. Pokušajte ponovo za nekoliko minuta.",
                    status = 429,
                }, ct);
            };

            // A general ceiling on anything that changes data, on top of the stricter per-endpoint limits below.
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var opt = http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                var writes = HttpMethods.IsPost(http.Request.Method) || HttpMethods.IsPut(http.Request.Method) ||
                             HttpMethods.IsPatch(http.Request.Method) || HttpMethods.IsDelete(http.Request.Method);
                // The payment webhook is authenticated by its signature, not by who calls it.
                var webhook = http.Request.Path.StartsWithSegments("/api/v1/payments/webhook");
                if (!opt.Enabled || !writes || webhook) return RateLimitPartition.GetNoLimiter("none");

                var who = http.User.FindFirstValue("sub") ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter($"writes:{who}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opt.WritesPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });

            void Add(string name, Func<RateLimitOptions, (int Permits, TimeSpan Window)> pick) =>
                limiter.AddPolicy(name, http =>
                {
                    var opt = http.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                    if (!opt.Enabled) return RateLimitPartition.GetNoLimiter("off");
                    var (permits, window) = pick(opt);
                    var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    return RateLimitPartition.GetFixedWindowLimiter($"{name}:{ip}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = window,
                        QueueLimit = 0,
                    });
                });

            Add(Policies.LoginLimit, o => (o.LoginPerMinute, TimeSpan.FromMinutes(1)));
            Add(Policies.RegisterLimit, o => (o.RegisterPerHour, TimeSpan.FromHours(1)));
            Add(Policies.ForgotLimit, o => (o.ForgotPerHour, TimeSpan.FromHours(1)));
            Add(Policies.RefreshLimit, o => (o.RefreshPerMinute, TimeSpan.FromMinutes(1)));
            Add(Policies.OrderLimit, o => (o.OrdersPerHour, TimeSpan.FromHours(1)));
        });

        return services;
    }
}
