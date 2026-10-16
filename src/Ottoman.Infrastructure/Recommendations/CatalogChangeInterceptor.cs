using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Ottoman.Application.Recommendations;
using Ottoman.Domain.Entities;

namespace Ottoman.Infrastructure.Recommendations;

/// <summary>
/// Drops the recommendation indexes after any save that touched the catalogue (products, services, barbers, brands,
/// categories). Doing it here, once, means no individual service can forget to invalidate. Bulk operations that bypass
/// SaveChanges (ExecuteUpdate/ExecuteDelete) call <see cref="IRecommendationCache.Invalidate"/> themselves.
/// </summary>
public sealed class CatalogChangeInterceptor(IRecommendationCache cache) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, object> _touched = [];

    private static bool IsCatalogue(object entity) =>
        entity is Product or Service or Brand or Category or EmployeeProfile or EmployeeService;

    private void Remember(DbContext? context)
    {
        if (context is null) return;
        var touched = context.ChangeTracker.Entries()
            .Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && IsCatalogue(e.Entity));
        if (touched) _touched.AddOrUpdate(context, new object());
    }

    private void Flush(DbContext? context)
    {
        if (context is not null && _touched.Remove(context)) cache.Invalidate();
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Remember(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Remember(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _touched.Remove(eventData.Context!);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) _touched.Remove(eventData.Context);
        return Task.CompletedTask;
    }
}
