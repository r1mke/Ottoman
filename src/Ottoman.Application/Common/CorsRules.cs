namespace Ottoman.Application.Common;

/// <summary>
/// Which web origins may call the API from a browser. The apps are native and need none of this, so the default
/// is no CORS at all. An origin is only accepted as an exact scheme://host[:port], never a wildcard.
/// </summary>
public static class CorsRules
{
    public static IReadOnlyList<string> Problems(IEnumerable<string> origins, bool isDevelopment)
    {
        var problems = new List<string>();
        foreach (var origin in origins.Where(o => !string.IsNullOrWhiteSpace(o)))
        {
            if (origin.Contains('*')) problems.Add($"Cors:Origins entry '{origin}' uses a wildcard; list exact origins.");
            else if (!TryNormalize(origin, isDevelopment, out _)) problems.Add($"Cors:Origins entry '{origin}' is not a valid origin (https://host[:port]).");
        }
        return problems;
    }

    /// <summary>Returns the origin without a trailing slash or path. Plain http is allowed only for localhost in development.</summary>
    public static bool TryNormalize(string origin, bool isDevelopment, out string normalized)
    {
        normalized = "";
        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.AbsolutePath != "/" || uri.Query != "" || uri.Fragment != "" || uri.UserInfo != "") return false;
        var secure = uri.Scheme == Uri.UriSchemeHttps;
        var devLocal = isDevelopment && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        if (!secure && !devLocal) return false;
        normalized = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string> origins, bool isDevelopment) =>
        origins.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => TryNormalize(o, isDevelopment, out var n) ? n : null).Where(n => n is not null).Select(n => n!).Distinct().ToList();
}
