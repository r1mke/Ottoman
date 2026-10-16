using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ottoman.Api.Controllers;

namespace Ottoman.Api.Filters;

/// <summary>
/// Answers 413 from the declared Content-Length before the multipart body is read. Without it, Kestrel's size
/// error is raised while model binding reads the form and MVC reports it as a generic 400.
/// Bodies without a declared length are still capped by Kestrel while they are read.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RejectOversizedUploadAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (context.HttpContext.Request.ContentLength is not > ImageUpload.RequestLimit) return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Zahtjev je prevelik.",
            Detail = $"Slika može imati najviše {ImageUpload.RequestLimit / 1_000_000} MB.",
        })
        { StatusCode = StatusCodes.Status413PayloadTooLarge };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
