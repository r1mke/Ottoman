using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ottoman.Application.Common;

namespace Ottoman.Api.Filters;

/// <summary>
/// Refuses (400) any request whose strings carry NUL, other control characters or bidi overrides (see
/// <see cref="TextSafety"/>): query values, route values and every string inside a JSON body, however deep.
/// Runs before validation so no handler ever sees such text.
/// </summary>
public sealed class TextSafetyFilter : IActionFilter
{
    private const int MaxDepth = 6;
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (name, value) in context.ActionArguments)
            Visit(value, name, 0, errors);
        if (errors.Count == 0) return;

        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
        {
            Title = "Podaci nisu ispravni.",
            Status = StatusCodes.Status400BadRequest,
        });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    /// <summary>Collects the path of every unsafe string under <paramref name="value"/>.</summary>
    public static void Visit(object? value, string path, int depth, Dictionary<string, string[]> errors)
    {
        switch (value)
        {
            case null:
                return;
            case string s:
                if (!TextSafety.IsSafe(s)) errors[path] = [TextSafety.Message];
                return;
            case IFormFile or Stream or CancellationToken:
                return;
        }
        if (depth >= MaxDepth) return;

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is decimal or DateTime or DateOnly or TimeOnly or DateTimeOffset or Guid or TimeSpan)
            return;

        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry e in dictionary)
            {
                Visit(e.Key as string, $"{path}.key", depth + 1, errors);
                Visit(e.Value, $"{path}[{e.Key}]", depth + 1, errors);
            }
            return;
        }
        if (value is IEnumerable items)
        {
            var i = 0;
            foreach (var item in items) Visit(item, $"{path}[{i++}]", depth + 1, errors);
            return;
        }

        // Only our own request types are walked, never framework objects.
        if (type.Namespace?.StartsWith("Ottoman", StringComparison.Ordinal) != true) return;
        foreach (var p in Properties.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(x => x.CanRead && x.GetIndexParameters().Length == 0).ToArray()))
            Visit(p.GetValue(value), $"{path}.{p.Name}", depth + 1, errors);
    }
}
