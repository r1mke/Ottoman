using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Ottoman.Api.Auth;
using Ottoman.Application.Audit;
using Ottoman.Domain;

namespace Ottoman.Api.Filters;

/// <summary>
/// Records who (staff) changed what, for every successful write, in one place so no endpoint can forget it.
/// It stores the action, the route and the ids from the URL, never the request body.
/// </summary>
public sealed class AuditFilter(IAuditLog audit) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is not null && !executed.ExceptionHandled) return;

        var http = context.HttpContext;
        var user = http.User;
        if (user.Identity?.IsAuthenticated != true) return;
        var isAdmin = user.IsInRole(Roles.Admin);
        var isStaff = isAdmin || user.IsInRole(Roles.Employee);
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor) return;

        var status = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? http.Response.StatusCode;
        if (!AuditPolicy.ShouldRecord(http.Request.Method, descriptor.ControllerName, status, isStaff)) return;

        // A create has no id in its URL yet; the Location it answers with does.
        var routeValues = (executed.Result as CreatedAtActionResult)?.RouteValues ?? context.RouteData.Values;
        var target = string.Join(", ", routeValues
            .Where(v => v.Key is not ("controller" or "action"))
            .Select(v => $"{v.Key}={v.Value}"));

        await audit.RecordAsync(new AuditRecord(
            user.UserId(),
            isAdmin ? Roles.Admin : Roles.Employee,
            http.Request.Method,
            $"{descriptor.ControllerName}.{descriptor.ActionName}",
            descriptor.AttributeRouteInfo?.Template ?? http.Request.Path.Value ?? "",
            target,
            status,
            http.Connection.RemoteIpAddress?.ToString()), CancellationToken.None);
    }
}
