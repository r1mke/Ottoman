using System.Globalization;
using System.Linq.Expressions;
using Ottoman.Domain;
using Ottoman.Domain.Entities;

namespace Ottoman.Infrastructure.Reviews;

/// <summary>Queries over reviews by target as the apps address it (type + key text), on the typed foreign keys.</summary>
public static class ReviewQueries
{
    /// <summary>
    /// The target key of a review as text, for grouping within one target type: the product or service id, or the
    /// barber's user id. Translates to SQL.
    /// </summary>
    public static readonly Expression<Func<Review, string>> TargetKey = r =>
        r.ProductId != null ? r.ProductId.Value.ToString() : r.ServiceId != null ? r.ServiceId.Value.ToString() : r.EmployeeProfileId!;

    /// <summary>The reviews of one target. A key that cannot name a target of that type matches nothing.</summary>
    public static IQueryable<Review> OfTarget(this IQueryable<Review> reviews, ReviewTargetType type, string key)
    {
        switch (type)
        {
            case ReviewTargetType.Product:
                return int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var product)
                    ? reviews.Where(r => r.ProductId == product) : reviews.Where(_ => false);
            case ReviewTargetType.Service:
                return int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var service)
                    ? reviews.Where(r => r.ServiceId == service) : reviews.Where(_ => false);
            default:
                return reviews.Where(r => r.EmployeeProfileId == key);
        }
    }

    /// <summary>The reviews of one kind of target (all products, all services or all barbers).</summary>
    public static IQueryable<Review> OfType(this IQueryable<Review> reviews, ReviewTargetType type) =>
        reviews.Where(r => r.TargetType == type);
}
