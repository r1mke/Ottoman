using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Application.Common;

namespace Ottoman.Api.Filters;

/// <summary>
/// Turns every exception that reaches the pipeline into a ProblemDetails answer in Bosnian. The expected failures
/// (<see cref="OttomanException"/>) keep their own status and message; anything else is a server fault: it is logged
/// in full here, while the client only gets a generic message with no stack trace, type name or infrastructure detail.
/// Registered after <see cref="HttpExceptionHandler"/>, so it is the last stop.
/// </summary>
public sealed class ApplicationExceptionHandler(IProblemDetailsService problems, ILogger<ApplicationExceptionHandler> log)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = ProblemTexts.Describe(exception);
        if (exception is OttomanException)
            log.LogInformation("{Method} {Path} refused with {Status}: {Message}", http.Request.Method, http.Request.Path, status, exception.Message);
        else
            log.LogError(exception, "Unhandled exception on {Method} {Path}", http.Request.Method, http.Request.Path);

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
