using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Catalog;
using Ottoman.Application.Employees;
using Ottoman.Application.Recommendations;
using Ottoman.Application.Shop;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Recommendations;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Recommendations;

/// <summary>An index plus the map from the engine's integer ids back to real keys (barbers are identified by text).</summary>
public sealed record CachedIndex(ItemIndex Index, IReadOnlyDictionary<int, string> KeyById);

/// <summary>Singleton holder of the built indexes, dropped explicitly whenever the catalogue changes (no TTL guessing).</summary>
public sealed class RecommendationCache : IRecommendationCache
{
    private readonly ConcurrentDictionary<string, CachedIndex> _indexes = new();

    public void Invalidate() => _indexes.Clear();

    public async Task<CachedIndex> GetOrBuildAsync(string kind, Func<Task<CachedIndex>> build)
    {
        if (_indexes.TryGetValue(kind, out var hit)) return hit;
        var built = await build();
        _indexes[kind] = built; // two concurrent builds produce identical results; the last write wins
        return built;
    }
}

public class RecommendationService(
    OttomanDbContext db,
    RecommendationCache cache,
    IProductCatalog products,
    IServiceCatalog services,
    IEmployeeService employees,
    TimeProvider clock) : IRecommendationService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- products

    public async Task<IReadOnlyList<Recommended<ProductDto>>> ProductsAsync(string userId, int count, CancellationToken ct)
    {
        var index = await ProductIndexAsync();
        var inStock = (await db.Products.AsNoTracking().Where(p => p.IsActive && p.Stock > 0).Select(p => p.Id).ToListAsync(ct)).ToHashSet();

        var orderLines = await db.Orders.AsNoTracking()
            .Where(o => o.ClientId == userId && (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Fulfilled))
            .SelectMany(o => o.Items.Select(i => new { i.ProductId, At = o.CreatedAtUtc })).ToListAsync(ct);
        var interactions = orderLines.Select(l => new Interaction(l.ProductId, 1.0, l.At)).ToList();
        interactions.AddRange(await ReviewInteractionsAsync(userId, ReviewTargetType.Product, ct));

        var bought = orderLines.Select(l => l.ProductId).ToHashSet();
        var popular = await PopularProductsAsync(ct);

        var ranked = Recommender.ForUser(index.Index, interactions, Now, inStock, bought, popular, count);
        return await WithProductReasonsAsync(ranked, ct);
    }

    public async Task<IReadOnlyList<Recommended<ProductDto>>> RelatedProductsAsync(int productId, int count, CancellationToken ct)
    {
        var index = await ProductIndexAsync();
        var inStock = (await db.Products.AsNoTracking().Where(p => p.IsActive && p.Stock > 0).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        var related = Recommender.Related(index.Index, productId, inStock, count);
        return await WithProductReasonsAsync(related, ct);
    }

    private Task<CachedIndex> ProductIndexAsync() => cache.GetOrBuildAsync("products", async () =>
    {
        var rows = await db.Products.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Description, Brand = p.Brand != null ? p.Brand.Name : null, Category = p.Category != null ? p.Category.Name : null, p.Price })
            .ToListAsync();
        var items = rows.Select(p => new ItemFeatures(p.Id, $"{p.Name} {p.Description}", p.Category, p.Brand, p.Price)).ToList();
        return new CachedIndex(ItemIndex.Build(items), rows.ToDictionary(p => p.Id, p => p.Id.ToString()));
    });

    private async Task<IReadOnlyList<int>> PopularProductsAsync(CancellationToken ct)
    {
        var sold = await db.OrderItems.AsNoTracking()
            .Where(i => db.Orders.Any(o => o.Id == i.OrderId && (o.Status == OrderStatus.Placed || o.Status == OrderStatus.Fulfilled)))
            .GroupBy(i => i.ProductId).Select(g => new { Id = g.Key, Qty = g.Sum(i => i.Quantity) }).ToListAsync(ct);
        var ratings = await RatingsAsync(ReviewTargetType.Product, ct);
        var all = await db.Products.AsNoTracking().Where(p => p.IsActive && p.Stock > 0).Select(p => p.Id).ToListAsync(ct);
        return all
            .Select(id => (Id: id, Score: (sold.FirstOrDefault(s => s.Id == id)?.Qty ?? 0) + Rating(ratings, id.ToString())))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Id).Select(x => x.Id).ToList();
    }

    // ---------------------------------------------------------------- services

    public async Task<IReadOnlyList<Recommended<ServiceDto>>> ServicesAsync(string userId, int count, CancellationToken ct)
    {
        var index = await ServiceIndexAsync();
        var active = await services.ListAsync(null, false, ct);
        var candidates = active.Select(s => s.Id).ToHashSet();

        var lines = await db.Reservations.AsNoTracking()
            .Where(r => r.ClientId == userId && (r.Status == ReservationStatus.Completed || r.Status == ReservationStatus.Confirmed))
            .SelectMany(r => r.Items.Select(i => new { i.ServiceId, At = r.StartUtc })).ToListAsync(ct);
        var interactions = lines.Select(l => new Interaction(l.ServiceId, 1.0, l.At)).ToList();
        interactions.AddRange(await ReviewInteractionsAsync(userId, ReviewTargetType.Service, ct));

        var counts = await db.ReservationItems.AsNoTracking()
            .Where(i => db.Reservations.Any(r => r.Id == i.ReservationId && r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.NoShow))
            .GroupBy(i => i.ServiceId).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct);
        var ratings = await RatingsAsync(ReviewTargetType.Service, ct);
        var popular = candidates
            .Select(id => (Id: id, Score: (counts.FirstOrDefault(c => c.Id == id)?.N ?? 0) + Rating(ratings, id.ToString())))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Id).Select(x => x.Id).ToList();

        // A service can be booked again, so what they already had is not hidden.
        var ranked = Recommender.ForUser(index.Index, interactions, Now, candidates, new HashSet<int>(), popular, count);
        var byId = active.ToDictionary(s => s.Id);
        var sourceIds = ranked.Select(r => r.Reason?.SourceItemId).OfType<int>().Distinct().ToList();
        var names = await db.Services.AsNoTracking().Where(s => sourceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return ranked.Where(r => byId.ContainsKey(r.Id))
            .Select(r => new Recommended<ServiceDto>(byId[r.Id], ReasonText.Compose(r.Reason ?? Reason.Popular, SourceName(names, r.Reason), RecommendedKind.Service)))
            .ToList();
    }

    private Task<CachedIndex> ServiceIndexAsync() => cache.GetOrBuildAsync("services", async () =>
    {
        var rows = await db.Services.AsNoTracking().Where(s => s.IsActive).Select(s => new { s.Id, s.Name, s.Description, s.Price }).ToListAsync();
        var items = rows.Select(s => new ItemFeatures(s.Id, $"{s.Name} {s.Description}", null, null, s.Price)).ToList();
        return new CachedIndex(ItemIndex.Build(items), rows.ToDictionary(s => s.Id, s => s.Id.ToString()));
    });

    // ---------------------------------------------------------------- barbers

    public async Task<IReadOnlyList<Recommended<EmployeeDto>>> EmployeesAsync(string userId, int count, CancellationToken ct)
    {
        var index = await EmployeeIndexAsync();
        var active = await employees.ListAsync(false, false, ct);
        var idByKey = index.KeyById.ToDictionary(kv => kv.Value, kv => kv.Key);
        var candidates = active.Where(e => idByKey.ContainsKey(e.Id)).Select(e => idByKey[e.Id]).ToHashSet();

        var visits = await db.Reservations.AsNoTracking()
            .Where(r => r.ClientId == userId && (r.Status == ReservationStatus.Completed || r.Status == ReservationStatus.Confirmed))
            .Select(r => new { r.EmployeeId, At = r.StartUtc }).ToListAsync(ct);
        var interactions = visits.Where(v => idByKey.ContainsKey(v.EmployeeId))
            .Select(v => new Interaction(idByKey[v.EmployeeId], 1.0, v.At)).ToList();
        foreach (var r in await ReviewInteractionsAsync(userId, ReviewTargetType.Employee, ct, idByKey)) interactions.Add(r);

        var jobs = await db.Reservations.AsNoTracking()
            .Where(r => r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.NoShow)
            .GroupBy(r => r.EmployeeId).Select(g => new { Key = g.Key, N = g.Count() }).ToListAsync(ct);
        var ratings = await RatingsAsync(ReviewTargetType.Employee, ct);
        var popular = active.Where(e => idByKey.ContainsKey(e.Id))
            .Select(e => (Id: idByKey[e.Id], Score: (jobs.FirstOrDefault(j => j.Key == e.Id)?.N ?? 0) + Rating(ratings, e.Id)))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Id).Select(x => x.Id).ToList();

        var ranked = Recommender.ForUser(index.Index, interactions, Now, candidates, new HashSet<int>(), popular, count);
        var byKey = active.ToDictionary(e => e.Id);
        var sourceKeys = ranked.Select(r => r.Reason?.SourceItemId).OfType<int>().Select(id => index.KeyById[id]).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => sourceKeys.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName }).ToListAsync(ct);
        var nameByKey = names.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
        return ranked
            .Select(r => new Recommended<EmployeeDto>(
                byKey[index.KeyById[r.Id]],
                ReasonText.Compose(
                    r.Reason ?? Reason.Popular,
                    r.Reason?.SourceItemId is { } s && nameByKey.TryGetValue(index.KeyById[s], out var name) ? name : null,
                    RecommendedKind.Barber)))
            .ToList();
    }

    /// <summary>A barber is described by what they do: their title, bio and the services they perform.</summary>
    private Task<CachedIndex> EmployeeIndexAsync() => cache.GetOrBuildAsync("employees", async () =>
    {
        var profiles = await db.EmployeeProfiles.AsNoTracking().Where(p => p.IsActive)
            .Select(p => new
            {
                p.UserId,
                p.Title,
                p.Bio,
                Services = p.Services.Where(s => s.Service.IsActive).Select(s => s.Service.Name + " " + s.Service.Description).ToList(),
            }).ToListAsync();
        var ordered = profiles.OrderBy(p => p.UserId, StringComparer.Ordinal).ToList();
        var items = ordered.Select((p, i) => new ItemFeatures(i + 1, $"{p.Title} {p.Bio} {string.Join(' ', p.Services)}")).ToList();
        return new CachedIndex(ItemIndex.Build(items), ordered.Select((p, i) => (Key: p.UserId, Id: i + 1)).ToDictionary(x => x.Id, x => x.Key));
    });


    private async Task<IReadOnlyList<Recommended<ProductDto>>> WithProductReasonsAsync(IReadOnlyList<Scored> ranked, CancellationToken ct)
    {
        var dtos = await products.GetManyAsync(ranked.Select(r => r.Id).ToList(), ct);
        var sourceIds = ranked.Select(r => r.Reason?.SourceItemId).OfType<int>().Distinct().ToList();
        var names = await db.Products.AsNoTracking().Where(p => sourceIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var byId = dtos.ToDictionary(d => d.Id);
        return ranked.Where(r => byId.ContainsKey(r.Id))
            .Select(r => new Recommended<ProductDto>(
                byId[r.Id], ReasonText.Compose(r.Reason ?? Reason.Popular, SourceName(names, r.Reason), RecommendedKind.Product)))
            .ToList();
    }

    private static string? SourceName(Dictionary<int, string> names, Reason? reason) =>
        reason?.SourceItemId is { } id && names.TryGetValue(id, out var name) ? name : null;
    // ---------------------------------------------------------------- shared

    private async Task<List<Interaction>> ReviewInteractionsAsync(
        string userId, ReviewTargetType type, CancellationToken ct, IReadOnlyDictionary<string, int>? idByKey = null)
    {
        var mine = await db.Reviews.AsNoTracking()
            .Where(r => r.ClientId == userId && r.TargetType == type && !r.IsHidden)
            .Select(r => new { r.TargetKey, r.Rating, r.CreatedAtUtc }).ToListAsync(ct);
        var result = new List<Interaction>();
        foreach (var r in mine)
        {
            var weight = Recommender.ReviewWeight(r.Rating);
            if (weight == 0) continue;
            if (idByKey is not null) { if (idByKey.TryGetValue(r.TargetKey, out var id)) result.Add(new Interaction(id, weight, r.CreatedAtUtc)); }
            else if (int.TryParse(r.TargetKey, out var numeric)) result.Add(new Interaction(numeric, weight, r.CreatedAtUtc));
        }
        return result;
    }

    private async Task<Dictionary<string, double>> RatingsAsync(ReviewTargetType type, CancellationToken ct) =>
        await db.Reviews.AsNoTracking().Where(r => r.TargetType == type && !r.IsHidden)
            .GroupBy(r => r.TargetKey).Select(g => new { g.Key, Avg = g.Average(r => (double)r.Rating) })
            .ToDictionaryAsync(x => x.Key, x => x.Avg, ct);

    private static double Rating(Dictionary<string, double> ratings, string key) => ratings.GetValueOrDefault(key);
}
