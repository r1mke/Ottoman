using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Ottoman.Api.Auth;
using Ottoman.Application.Idempotency;

namespace Ottoman.Api.Filters;

/// <summary>
/// Makes a create endpoint safe to repeat. With an <c>Idempotency-Key</c> header, a second identical request returns
/// the first answer instead of creating a second order or booking. Without the header nothing changes.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute() : TypeFilterAttribute(typeof(IdempotencyFilter));

public sealed class IdempotencyFilter(IIdempotencyStore store, IOptions<JsonOptions> json) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!http.Request.Headers.TryGetValue(IdempotencyRules.Header, out var values))
        {
            await next();
            return;
        }

        var key = values.ToString();
        if (!IdempotencyRules.IsValidKey(key))
        {
            context.Result = Problem(http, StatusCodes.Status400BadRequest, "Neispravan Idempotency-Key.",
                "Ključ smije imati najviše 64 znaka: slova, cifre, - _ : .");
            return;
        }

        var userId = http.User.UserId();
        var route = $"{http.Request.Method} {(context.ActionDescriptor as ControllerActionDescriptor)?.AttributeRouteInfo?.Template ?? http.Request.Path.Value}";
        var options = json.Value.JsonSerializerOptions;
        var arguments = context.ActionArguments.Where(a => a.Value is not CancellationToken).OrderBy(a => a.Key)
            .ToDictionary(a => a.Key, a => a.Value);
        var hash = IdempotencyRules.Hash(JsonSerializer.Serialize(arguments, options));

        var begin = await store.BeginAsync(userId, key, route, hash, http.RequestAborted);
        switch (begin.Outcome)
        {
            case BeginOutcome.Replay:
                http.Response.Headers["Idempotent-Replayed"] = "true";
                context.Result = new ContentResult { StatusCode = begin.StatusCode, Content = begin.Body, ContentType = begin.ContentType };
                return;
            case BeginOutcome.InProgress:
                context.Result = Problem(http, StatusCodes.Status409Conflict, "Zahtjev se još obrađuje.",
                    "Isti zahtjev je već u toku. Sačekajte odgovor prvog.");
                return;
            case BeginOutcome.Mismatch:
                context.Result = Problem(http, StatusCodes.Status422UnprocessableEntity, "Ključ je već upotrijebljen.",
                    "Isti Idempotency-Key je poslan uz drugačiji zahtjev.");
                return;
        }

        ActionExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch
        {
            await store.AbandonAsync(userId, key, CancellationToken.None);
            throw;
        }

        var status = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? StatusCodes.Status200OK;
        if (executed.Exception is null && executed.Result is ObjectResult { Value: not null } ok && status is >= 200 and < 300)
        {
            await store.CompleteAsync(userId, key, status, JsonSerializer.Serialize(ok.Value, ok.Value.GetType(), options),
                "application/json; charset=utf-8", CancellationToken.None);
        }
        else
        {
            // Failures are not remembered: the caller may fix the problem and retry with the same key.
            await store.AbandonAsync(userId, key, CancellationToken.None);
        }
    }

    private static ObjectResult Problem(HttpContext http, int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = http.Request.Path }) { StatusCode = status };
}
