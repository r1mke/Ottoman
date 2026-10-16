using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Outbox;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class HardeningTests(ApiFixture api)
{
    [Fact]
    public async Task Every_response_carries_the_defensive_headers()
    {
        var res = await api.CreateClient().GetAsync("/health/live");
        res.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        res.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        res.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        res.Headers.CacheControl!.NoStore.Should().BeTrue();

        // errors too, not only the happy path
        var denied = await api.CreateClient().GetAsync("/api/v1/products");
        denied.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        denied.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Fact]
    public async Task A_failed_login_says_the_same_thing_for_a_missing_and_a_wrong_account()
    {
        var (email, _) = await api.CreateClient().NewClient();
        var missing = await api.CreateClient().Login($"nobody{Guid.NewGuid():N}@test.local", "Whatever123!");
        var wrong = await api.CreateClient().Login(email, "Wrong-password1!");

        missing.StatusCode.Should().Be(wrong.StatusCode);
        (await missing.Content.ReadAsStringAsync()).Should().Be(await wrong.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cleanup_removes_old_delivered_rows_and_reset_tokens_but_never_undelivered_ones()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var now = DateTime.UtcNow;
        var tag = Guid.NewGuid().ToString("N");

        OutboxMessage Msg(string type, DateTime? processed) => new()
        {
            Type = type,
            PayloadJson = $"{{\"tag\":\"{tag}\"}}",
            CreatedAtUtc = now.AddDays(-40),
            ProcessedAtUtc = processed,
        };
        var oldDelivered = Msg("OrderPaid", now.AddDays(-8));
        var freshDelivered = Msg("OrderPaid", now.AddHours(-2));
        var oldReset = Msg("PasswordResetRequested", now.AddDays(-2));
        var freshReset = Msg("PasswordResetRequested", now.AddHours(-3));
        var neverDelivered = Msg("OrderPaid", null);
        db.OutboxMessages.AddRange(oldDelivered, freshDelivered, oldReset, freshReset, neverDelivered);
        db.ProcessedEvents.Add(new ProcessedEvent { OutboxMessageId = 900_000_000_001, ProcessedAtUtc = now.AddDays(-31) });
        db.ProcessedEvents.Add(new ProcessedEvent { OutboxMessageId = 900_000_000_002, ProcessedAtUtc = now.AddDays(-1) });
        await db.SaveChangesAsync();

        var removed = await scope.ServiceProvider.GetRequiredService<OutboxCleanup>().PurgeAsync(default);

        removed.Should().BeGreaterThanOrEqualTo(3);
        var left = (await db.OutboxMessages.AsNoTracking().Where(m => m.PayloadJson.Contains(tag)).Select(m => m.Id).ToListAsync()).ToHashSet();
        left.Should().BeEquivalentTo(new[] { freshDelivered.Id, freshReset.Id, neverDelivered.Id });
        (await db.ProcessedEvents.AnyAsync(p => p.OutboxMessageId == 900_000_000_001)).Should().BeFalse();
        (await db.ProcessedEvents.AnyAsync(p => p.OutboxMessageId == 900_000_000_002)).Should().BeTrue();
    }

    [Fact]
    public async Task Every_endpoint_requires_a_signed_in_user_unless_it_explicitly_opts_out()
    {
        var fallback = await api.Services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        fallback.Should().NotBeNull("an endpoint without attributes must not be public");

        // The only public endpoints are the ones that say so.
        var sources = api.Services.GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var open = sources.SelectMany(s => s.Endpoints)
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null)
            .Select(e => (e as Microsoft.AspNetCore.Routing.RouteEndpoint)?.RoutePattern.RawText)
            .OrderBy(x => x).ToList();

        open.Should().BeEquivalentTo(new[]
        {
            "api/v1/auth/register", "api/v1/auth/login", "api/v1/auth/refresh", "api/v1/auth/logout",
            "api/v1/auth/forgot-password", "api/v1/auth/reset-password", "api/v1/payments/webhook",
            "/health/live", "/health/ready",
            // API docs: only mapped in Development
            "/openapi/{documentName}.json", "/scalar/{documentName?}", "/scalar/favicon.svg", "/scalar/scalar.aspnetcore.js", "/scalar/scalar.js",
        }, "adding a public endpoint must be a conscious decision that updates this list");
    }

    [Fact]
    public async Task Unknown_json_properties_cannot_set_server_owned_fields()
    {
        // Mass-assignment probe: price, status, owner and role are not part of the request contracts.
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var res = await client.PutAsJsonAsync("/api/v1/me", new
        {
            firstName = "Ana",
            lastName = "Anić",
            phone = (string?)null,
            roles = new[] { "Admin" },
            isBlocked = false,
            id = "someone-else",
            email = "evil@test.local",
        });

        var me = await client.GetFromJsonAsync<Ottoman.Application.Auth.UserDto>("/api/v1/me");
        me!.Roles.Should().BeEquivalentTo("Client");
        me.Id.Should().Be(auth.User.Id);
        me.Email.Should().NotBe("evil@test.local");
        res.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }
}

public class HttpExceptionHandlerTests
{
    private sealed class Recorder : Microsoft.AspNetCore.Http.IProblemDetailsService
    {
        public Microsoft.AspNetCore.Http.ProblemDetailsContext? Written;

        public ValueTask WriteAsync(Microsoft.AspNetCore.Http.ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(Microsoft.AspNetCore.Http.ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.FromResult(true);
        }
    }

    [Fact]
    public async Task A_request_that_is_too_large_is_a_413_not_a_server_error()
    {
        var recorder = new Recorder();
        var handler = new Ottoman.Api.Filters.HttpExceptionHandler(recorder);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            http, new Microsoft.AspNetCore.Http.BadHttpRequestException("Request body too large.", 413), default);

        handled.Should().BeTrue();
        http.Response.StatusCode.Should().Be(413);
        recorder.Written!.ProblemDetails.Status.Should().Be(413);
    }

    [Fact]
    public async Task Real_server_failures_are_left_to_the_default_handler()
    {
        var handler = new Ottoman.Api.Filters.HttpExceptionHandler(new Recorder());
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();

        (await handler.TryHandleAsync(http, new InvalidOperationException("boom"), default)).Should().BeFalse();
    }
}
