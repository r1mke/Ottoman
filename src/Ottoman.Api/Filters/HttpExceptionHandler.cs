using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace Ottoman.Api.Filters;

/// <summary>
/// Errors caused by the caller's request (body too large, malformed HTTP) are the caller's problem, not a server
/// failure: answer with the status Kestrel chose (413, 400, ...) instead of a 500 that pages somebody at night.
/// Everything else falls through to the default handler.
/// </summary>
public sealed class HttpExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        if (exception is not BadHttpRequestException bad) return false;

        http.Response.StatusCode = bad.StatusCode;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = bad.StatusCode,
                Title = bad.StatusCode == StatusCodes.Status413PayloadTooLarge ? "Zahtjev je prevelik." : "Neispravan zahtjev.",
            },
        });
    }
}
