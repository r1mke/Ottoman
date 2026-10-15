using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ottoman.Api.Filters;

/// <summary>Runs the registered FluentValidation validator for every action argument and returns RFC 7807 400 on failure.</summary>
public class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = string.IsNullOrEmpty(failure.PropertyName) ? "request" : failure.PropertyName;
                if (!errors.TryGetValue(key, out var list)) errors[key] = list = [];
                list.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(
                errors.ToDictionary(k => k.Key, k => k.Value.ToArray()))
            {
                Title = "Podaci nisu ispravni.",
                Status = StatusCodes.Status400BadRequest,
            });
            return;
        }

        await next();
    }
}
