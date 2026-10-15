using Microsoft.AspNetCore.Mvc;
using Ottoman.Application.Common;

namespace Ottoman.Api.Controllers;

public static class ResultExtensions
{
    /// <summary>Maps an application <see cref="Result{T}"/> to an HTTP response (RFC 7807 for failures).</summary>
    public static ActionResult<T> ToActionResult<T>(this ControllerBase c, Result<T> result) =>
        result.Status switch
        {
            ResultStatus.Ok => result.Value!,
            ResultStatus.NotFound => Problem(c, StatusCodes.Status404NotFound, "Nije pronađeno.", result.Error),
            ResultStatus.Conflict => Problem(c, StatusCodes.Status409Conflict, "Sukob podataka.", result.Error),
            _ => Problem(c, StatusCodes.Status400BadRequest, "Podaci nisu ispravni.", result.Error),
        };

    public static IActionResult ToNoContent<T>(this ControllerBase c, Result<T> result) =>
        result.Succeeded ? c.NoContent() : c.ToActionResult(result).Result!;

    private static ObjectResult Problem(ControllerBase c, int status, string title, string? detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
