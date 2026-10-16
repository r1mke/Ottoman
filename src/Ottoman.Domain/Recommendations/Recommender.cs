namespace Ottoman.Domain.Recommendations;

/// <summary>What the engine knows about one thing that can be recommended (a product, a service or a barber).</summary>
public sealed record ItemFeatures(int Id, string Text, string? Category = null, string? Brand = null, decimal? Price = null);

/// <summary>Something the user did with an item. Positive weights pull recommendations towards it, negative push away.</summary>
public sealed record Interaction(int ItemId, double Weight, DateTime AtUtc);

/// <summary>A sparse feature vector. Compared with cosine similarity, so only direction matters.</summary>
public sealed class SparseVector
{
    private readonly Dictionary<string, double> _w;

    public SparseVector(Dictionary<string, double> weights) => _w = weights;

    public static SparseVector Empty => new([]);

    public bool IsEmpty => _w.Count == 0;
    public IReadOnlyDictionary<string, double> Weights => _w;

    public double Norm => Math.Sqrt(_w.Values.Sum(v => v * v));

    public double Dot(SparseVector other)
    {
        var (small, large) = _w.Count <= other._w.Count ? (_w, other._w) : (other._w, _w);
        var sum = 0.0;
        foreach (var (k, v) in small)
            if (large.TryGetValue(k, out var o)) sum += v * o;
        return sum;
    }

    public double Cosine(SparseVector other)
    {
        var denominator = Norm * other.Norm;
        return denominator < 1e-12 ? 0 : Dot(other) / denominator;
    }

    public SparseVector Scaled(double factor) => new(_w.ToDictionary(kv => kv.Key, kv => kv.Value * factor));

    public SparseVector Normalized()
    {
        var n = Norm;
        return n < 1e-12 ? this : Scaled(1 / n);
    }

    /// <summary>Adds <paramref name="other"/> scaled by <paramref name="factor"/> into this vector (in place).</summary>
    public void AddScaled(SparseVector other, double factor)
    {
        foreach (var (k, v) in other._w)
            _w[k] = _w.GetValueOrDefault(k) + v * factor;
    }
}

/// <summary>
/// TF-IDF vectors for a catalogue. Words are weighted by how rare they are in this catalogue; category, brand and a
/// coarse price band are added as fixed-weight features so "same brand" and "similar price" count as well as shared words.
/// </summary>
public sealed class ItemIndex
{
    public const double CategoryWeight = 1.5;
    public const double BrandWeight = 1.0;
    public const double PriceBandWeight = 0.5;

    private readonly Dictionary<int, SparseVector> _vectors;

    private ItemIndex(Dictionary<int, SparseVector> vectors) => _vectors = vectors;

    public IReadOnlyDictionary<int, SparseVector> Vectors => _vectors;

    public static ItemIndex Build(IReadOnlyList<ItemFeatures> items)
    {
        var tokenized = items.ToDictionary(i => i.Id, i => TextNormalizer.Tokens(i.Text));
        var n = items.Count;

        var documentFrequency = new Dictionary<string, int>();
        foreach (var tokens in tokenized.Values)
            foreach (var t in tokens.Distinct())
                documentFrequency[t] = documentFrequency.GetValueOrDefault(t) + 1;

        var priceBands = PriceBands(items);
        var vectors = new Dictionary<int, SparseVector>(n);
        foreach (var item in items)
        {
            var w = new Dictionary<string, double>();
            foreach (var group in tokenized[item.Id].GroupBy(t => t))
            {
                var tf = 1 + Math.Log(group.Count());
                var idf = Math.Log((n + 1.0) / (documentFrequency[group.Key] + 1.0)) + 1;
                w["t:" + group.Key] = tf * idf;
            }

            // Words are scaled to unit length first so the fixed weights below mean the same for a short and a long description.
            var vector = new SparseVector(w).Normalized();
            if (!string.IsNullOrWhiteSpace(item.Category))
                vector.AddScaled(new SparseVector(new() { ["c:" + Key(item.Category)] = 1 }), CategoryWeight);
            if (!string.IsNullOrWhiteSpace(item.Brand))
                vector.AddScaled(new SparseVector(new() { ["b:" + Key(item.Brand)] = 1 }), BrandWeight);
            if (priceBands.TryGetValue(item.Id, out var band))
                vector.AddScaled(new SparseVector(new() { ["p:" + band] = 1 }), PriceBandWeight);

            vectors[item.Id] = vector;
        }
        return new ItemIndex(vectors);
    }

    private static string Key(string s) => TextNormalizer.StripDiacritics(s).Trim().ToLowerInvariant();

    /// <summary>Low / mid / high thirds of the catalogue by price. With fewer than three priced items there is no band.</summary>
    private static Dictionary<int, int> PriceBands(IReadOnlyList<ItemFeatures> items)
    {
        var priced = items.Where(i => i.Price.HasValue).OrderBy(i => i.Price).ThenBy(i => i.Id).ToList();
        var bands = new Dictionary<int, int>();
        if (priced.Count < 3) return bands;
        for (var i = 0; i < priced.Count; i++)
            bands[priced[i].Id] = Math.Min(2, i * 3 / priced.Count);
        return bands;
    }
}

public sealed record Scored(int Id, double Score);

public static class Recommender
{
    public const double HalfLifeDays = 90;

    /// <summary>Recent behaviour counts more: an interaction loses half its weight every <see cref="HalfLifeDays"/> days.</summary>
    public static double Decay(DateTime interactionUtc, DateTime nowUtc)
    {
        var ageDays = Math.Max(0, (nowUtc - interactionUtc).TotalDays);
        return Math.Pow(0.5, ageDays / HalfLifeDays);
    }

    /// <summary>The user's taste: the weighted sum of the items they interacted with.</summary>
    public static SparseVector UserProfile(ItemIndex index, IEnumerable<Interaction> interactions, DateTime nowUtc)
    {
        var profile = SparseVector.Empty;
        foreach (var i in interactions)
        {
            if (!index.Vectors.TryGetValue(i.ItemId, out var v)) continue; // item no longer in the catalogue
            profile.AddScaled(v, i.Weight * Decay(i.AtUtc, nowUtc));
        }
        return profile;
    }

    /// <summary>
    /// Items ranked by similarity to the user's taste. <paramref name="candidates"/> are the items that may be shown
    /// (active, in stock); <paramref name="exclude"/> are ones to hide (already bought). With no usable history the
    /// <paramref name="popular"/> list is returned instead ("cold start"), never random items.
    /// </summary>
    public static IReadOnlyList<Scored> ForUser(
        ItemIndex index,
        IReadOnlyCollection<Interaction> interactions,
        DateTime nowUtc,
        IReadOnlySet<int> candidates,
        IReadOnlySet<int> exclude,
        IReadOnlyList<int> popular,
        int count)
    {
        var profile = UserProfile(index, interactions, nowUtc);

        var ranked = candidates
            .Where(id => !exclude.Contains(id) && index.Vectors.ContainsKey(id))
            .Select(id => new Scored(id, profile.Cosine(index.Vectors[id])))
            .Where(s => s.Score > 1e-9)
            .OrderByDescending(s => s.Score).ThenBy(s => s.Id)
            .Take(count)
            .ToList();
        if (ranked.Count > 0) return ranked;

        // Popular items, but never ones that resemble something the user marked as disliked.
        return popular
            .Where(id => candidates.Contains(id) && !exclude.Contains(id))
            .Where(id => !index.Vectors.TryGetValue(id, out var v) || profile.Cosine(v) >= -1e-9)
            .Take(count).Select(id => new Scored(id, 0)).ToList();
    }

    /// <summary>"Similar items": the nearest neighbours of one item, never the item itself.</summary>
    public static IReadOnlyList<Scored> Related(ItemIndex index, int itemId, IReadOnlySet<int> candidates, int count)
    {
        if (!index.Vectors.TryGetValue(itemId, out var anchor)) return [];
        return candidates
            .Where(id => id != itemId && index.Vectors.ContainsKey(id))
            .Select(id => new Scored(id, anchor.Cosine(index.Vectors[id])))
            .Where(s => s.Score > 1e-9)
            .OrderByDescending(s => s.Score).ThenBy(s => s.Id)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Interaction weight for a review: a good rating is a stronger signal than a purchase, and a bad one outweighs the purchase it follows (bought it, hated it: net negative).
    /// Middling ratings (3) say nothing either way.
    /// </summary>
    public static double ReviewWeight(int rating) => rating switch
    {
        >= 4 => 1.5,
        <= 2 => -2.0,
        _ => 0,
    };
}
