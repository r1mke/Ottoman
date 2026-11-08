using Microsoft.AspNetCore.Mvc;
using Ottoman.Application.Common;

namespace Ottoman.Api.Controllers;

public static class ResultExtensions
{
    /// <summary>Maps an application <see cref="Result{T}"/> to an HTTP response (RFC 7807 for failures).</summary>
    public static ActionResult<T> ToActionResult<T>(this ControllerBase c, Result<T> result)
    {
        if (result.Succeeded) return result.Value!;
        var (status, title) = ProblemTexts.Describe(result.Status);
        return Problem(status, title, result.Error);
    }

    public static IActionResult ToNoContent<T>(this ControllerBase c, Result<T> result) =>
        result.Succeeded ? c.NoContent() : c.ToActionResult(result).Result!;

    private static ObjectResult Problem(int status, string title, string? detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}

public static class ImageUpload
{
    /// <summary>Picture limit (5 MB) plus multipart overhead. The global cap is 1 MB, so upload actions raise it explicitly.</summary>
    public const int RequestLimit = 6_000_000;

    /// <summary>Slightly above the request cap so Kestrel (413) trips before the form reader (400).</summary>
    public const int FormLimit = RequestLimit + 1_000_000;

    /// <summary>Carries the failure of a step that returned nothing over to a response that returns the fresh resource.</summary>
    public static Result<T> As<T>(this Result<bool> failed) => new(failed.Status, default, failed.Error);
}

public static class PagingExtensions
{
    public const string TotalCountHeader = "X-Total-Count";

    /// <summary>
    /// The page of items as a plain JSON array, with the total in <c>X-Total-Count</c>. Small lookup lists (services,
    /// barbers, brands) stay easy to bind in forms while still being limited to <c>PageQuery.MaxPageSize</c> rows.
    /// </summary>
    public static IReadOnlyList<T> Page<T>(this ControllerBase c, PagedResult<T> page)
    {
        c.Response.Headers[TotalCountHeader] = page.TotalCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        c.Response.Headers.AccessControlExposeHeaders = TotalCountHeader;
        return page.Items;
    }
}
