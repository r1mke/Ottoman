using Ottoman.Domain.Recommendations;

namespace Ottoman.Application.Recommendations;

public enum RecommendedKind
{
    Product,
    Service,
    Barber,
}

/// <summary>The explanation shown with a recommendation, in words the client understands.</summary>
public record RecommendationReason(string Kind, string? SourceName, string Text);

public record Recommended<T>(T Item, RecommendationReason Reason);

/// <summary>Turns the engine's <see cref="Reason"/> into one sentence. Pure; every fact in it comes from the comparison.</summary>
public static class ReasonText
{
    public static RecommendationReason Compose(Reason reason, string? sourceName, RecommendedKind kind)
    {
        var text = reason.Kind switch
        {
            ReasonKind.Popular => "Popularno u salonu. Čim nešto kupite ili rezervišete, preporuke će biti lične.",
            ReasonKind.SimilarToItem => With($"Slično kao „{sourceName}“", Details(reason)),
            _ => With(Opening(kind, sourceName), Details(reason)),
        };
        return new RecommendationReason(reason.Kind.ToString(), sourceName, text);
    }

    private static string Opening(RecommendedKind kind, string? name) => (kind, name) switch
    {
        (_, null or "") => "Slično onome što ste već birali",
        (RecommendedKind.Product, _) => $"Jer ste kupili „{name}“",
        (RecommendedKind.Service, _) => $"Jer ste koristili uslugu „{name}“",
        _ => $"Jer ste bili kod frizera {name}",
    };

    private static string Details(Reason r)
    {
        var parts = new List<string>();
        if (r.SharedCategory is { Length: > 0 }) parts.Add($"ista kategorija ({r.SharedCategory})");
        if (r.SharedBrand is { Length: > 0 }) parts.Add($"isti brend ({r.SharedBrand})");
        if (r.SharedWords is { Count: > 0 }) parts.Add($"slični pojmovi: {string.Join(", ", r.SharedWords)}");
        if (r.SamePriceBand) parts.Add("slična cijena");
        return string.Join(", ", parts);
    }

    private static string With(string opening, string details) => details.Length == 0 ? opening : $"{opening} - {details}";
}
