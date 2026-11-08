using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Ottoman.Api.Filters;

/// <summary>
/// The 400 for a request that could not even be read: malformed JSON, an unknown enum name, "abc" for a number or a
/// date. ASP.NET's own texts are English and name .NET types ("could not be converted to Ottoman.Domain..."), so every
/// message is replaced by a Bosnian one; only the field names stay, so the apps can still point at the field.
/// </summary>
public static class InvalidModelState
{
    public const string Title = "Podaci nisu ispravni.";
    public const string BodyMessage = "Zahtjev nije ispravan (provjerite JSON).";
    public const string ValueMessage = "Vrijednost nije ispravna.";

    /// <param name="bodyNames">Names of the action's body parameters: an error on one of them is about the whole body.</param>
    public static ValidationProblemDetails Problem(ModelStateDictionary state, IReadOnlySet<string> bodyNames)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (rawKey, entry) in state)
        {
            if (entry.Errors.Count == 0) continue;
            var key = FieldName(rawKey);
            var whole = key.Length == 0 || bodyNames.Contains(key);
            errors[whole ? "request" : key] = [whole ? BodyMessage : ValueMessage];
        }
        if (errors.Count == 0) errors["request"] = [BodyMessage];
        return new ValidationProblemDetails(errors) { Title = Title, Status = StatusCodes.Status400BadRequest };
    }

    /// <summary>"$.items[0].quantity" becomes "items[0].quantity"; "$" (the whole body) becomes "".</summary>
    public static string FieldName(string key)
    {
        if (key == "$") return "";
        return key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
    }

    public static IActionResult Respond(ActionContext context)
    {
        var bodyNames = context.ActionDescriptor.Parameters
            .Where(p => p.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        return new BadRequestObjectResult(Problem(context.ModelState, bodyNames))
        {
            ContentTypes = { "application/problem+json" },
        };
    }
}
