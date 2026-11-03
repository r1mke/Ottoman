using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Reviews;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Reviews;

public class ReviewService(OttomanDbContext db, TimeProvider clock) : IReviewService
{
    // ---------------------------------------------------------------- reading

    public async Task<ReviewListDto> ListAsync(Actor viewer, ReviewTargetType type, string key, bool includeHidden, PageQuery page, CancellationToken ct)
    {
        var showHidden = includeHidden && viewer.IsAdmin;
        var visible = db.Reviews.AsNoTracking().Where(r => r.TargetType == type && r.TargetKey == key && !r.IsHidden);
        var count = await visible.CountAsync(ct);
        var average = count == 0 ? 0 : await visible.AverageAsync(r => (double)r.Rating, ct);

        var query = db.Reviews.AsNoTracking().Where(r => r.TargetType == type && r.TargetKey == key);
        if (!showHidden) query = query.Where(r => !r.IsHidden);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);

        return new ReviewListDto(await ToDtosAsync(rows, viewer.UserId, ct), total, Math.Round(average, 2), count);
    }

    public async Task<IReadOnlyList<ReviewSummaryDto>> SummaryAsync(ReviewTargetType type, CancellationToken ct)
    {
        var rows = await db.Reviews.AsNoTracking().Where(r => r.TargetType == type && !r.IsHidden)
            .GroupBy(r => r.TargetKey)
            .Select(g => new { Key = g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync(ct);
        return rows.Select(r => new ReviewSummaryDto(r.Key, Math.Round(r.Average, 2), r.Count)).ToList();
    }

    public async Task<PagedResult<ReviewSummaryDto>> SummaryPageAsync(ReviewTargetType type, PageQuery page, CancellationToken ct)
    {
        var groups = db.Reviews.AsNoTracking().Where(r => r.TargetType == type && !r.IsHidden)
            .GroupBy(r => r.TargetKey)
            .Select(g => new { Key = g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() });
        var total = await groups.CountAsync(ct);
        var rows = await groups.OrderBy(g => g.Key).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<ReviewSummaryDto>(rows.Select(r => new ReviewSummaryDto(r.Key, Math.Round(r.Average, 2), r.Count)).ToList(), total);
    }

    public async Task<PagedResult<ReviewDto>> ListAllAsync(bool onlyHidden, PageQuery page, CancellationToken ct)
    {
        var query = db.Reviews.AsNoTracking().AsQueryable();
        if (onlyHidden) query = query.Where(r => r.IsHidden);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<ReviewDto>(await ToDtosAsync(rows, "", ct), total);
    }

    // ---------------------------------------------------------------- eligibility

    public async Task<EligibilityDto> EligibilityAsync(Actor client, ReviewTargetType type, string key, CancellationToken ct)
    {
        if (!client.IsClient) return new EligibilityDto(false, "Recenzije pišu samo klijenti.", null);
        // Only the canonical spelling exists: "05" or " 5" would otherwise be a second, separate target.
        if (ReviewTargetKey.Canonical(type, key) is null) return new EligibilityDto(false, "Stavka nije pronađena.", null);

        var existing = await db.Reviews.AsNoTracking()
            .Where(r => r.ClientId == client.UserId && r.TargetType == type && r.TargetKey == key)
            .Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
        if (existing is not null) return new EligibilityDto(false, "Već ste ostavili recenziju.", existing);

        if (!await TargetExistsAsync(type, key, ct)) return new EligibilityDto(false, "Stavka nije pronađena.", null);
        if (!await UsedAsync(client.UserId, type, key, ct))
            return new EligibilityDto(false, type == ReviewTargetType.Product
                ? "Recenziju možete ostaviti nakon preuzimanja proizvoda."
                : "Recenziju možete ostaviti nakon završenog termina.", null);
        return new EligibilityDto(true, null, null);
    }

    private async Task<bool> TargetExistsAsync(ReviewTargetType type, string key, CancellationToken ct) => type switch
    {
        ReviewTargetType.Employee => await db.EmployeeProfiles.AnyAsync(p => p.UserId == key, ct),
        ReviewTargetType.Service => int.TryParse(key, out var s) && await db.Services.AnyAsync(x => x.Id == s, ct),
        ReviewTargetType.Product => int.TryParse(key, out var p) && await db.Products.AnyAsync(x => x.Id == p, ct),
        _ => false,
    };

    /// <summary>A review must come from someone who really had the appointment or collected the goods.</summary>
    private async Task<bool> UsedAsync(string clientId, ReviewTargetType type, string key, CancellationToken ct) => type switch
    {
        ReviewTargetType.Employee => await db.Reservations.AnyAsync(
            r => r.ClientId == clientId && r.EmployeeId == key && r.Status == ReservationStatus.Completed, ct),
        ReviewTargetType.Service => int.TryParse(key, out var s) && await db.Reservations.AnyAsync(
            r => r.ClientId == clientId && r.Status == ReservationStatus.Completed && r.Items.Any(i => i.ServiceId == s), ct),
        ReviewTargetType.Product => int.TryParse(key, out var p) && await db.Orders.AnyAsync(
            o => o.ClientId == clientId && o.Status == OrderStatus.Fulfilled && o.Items.Any(i => i.ProductId == p), ct),
        _ => false,
    };

    // ---------------------------------------------------------------- writing

    public async Task<Result<ReviewDto>> CreateAsync(Actor client, CreateReviewRequest r, CancellationToken ct)
    {
        var eligibility = await EligibilityAsync(client, r.TargetType, r.TargetKey, ct);
        if (!eligibility.CanReview)
            return eligibility.ExistingReviewId is not null
                ? Result<ReviewDto>.Conflict(eligibility.Reason!)
                : Result<ReviewDto>.Invalid(eligibility.Reason!);

        var review = new Review
        {
            ClientId = client.UserId,
            TargetType = r.TargetType,
            TargetKey = r.TargetKey,
            Rating = r.Rating,
            Comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment.Trim(),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Reviews.Add(review);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return Result<ReviewDto>.Conflict("Već ste ostavili recenziju.");
        }
        return Result<ReviewDto>.Ok((await ToDtosAsync([review], client.UserId, ct)).Single());
    }

    public async Task<Result<ReviewDto>> UpdateAsync(Actor client, int id, UpdateReviewRequest r, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id && x.ClientId == client.UserId, ct);
        if (review is null) return Result<ReviewDto>.NotFound("Recenzija nije pronađena.");

        review.Rating = r.Rating;
        review.Comment = string.IsNullOrWhiteSpace(r.Comment) ? null : r.Comment.Trim();
        review.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Result<ReviewDto>.Ok((await ToDtosAsync([review], client.UserId, ct)).Single());
    }

    public async Task<Result<bool>> DeleteAsync(Actor actor, int id, CancellationToken ct)
    {
        var rows = await db.Reviews.Where(x => x.Id == id && (actor.IsAdmin || x.ClientId == actor.UserId)).ExecuteDeleteAsync(ct);
        return rows == 1 ? Result<bool>.Ok(true) : Result<bool>.NotFound("Recenzija nije pronađena.");
    }

    public async Task<Result<ReviewDto>> SetHiddenAsync(int id, bool hidden, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (review is null) return Result<ReviewDto>.NotFound("Recenzija nije pronađena.");
        review.IsHidden = hidden;
        await db.SaveChangesAsync(ct);
        return Result<ReviewDto>.Ok((await ToDtosAsync([review], "", ct)).Single());
    }

    public async Task<Result<ReviewDto>> ReplyAsync(int id, string text, CancellationToken ct)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (review is null) return Result<ReviewDto>.NotFound("Recenzija nije pronađena.");
        review.ReplyText = text.Trim();
        review.ReplyAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Result<ReviewDto>.Ok((await ToDtosAsync([review], "", ct)).Single());
    }

    // ---------------------------------------------------------------- mapping

    private async Task<IReadOnlyList<ReviewDto>> ToDtosAsync(List<Review> rows, string viewerId, CancellationToken ct)
    {
        var authorIds = rows.Select(r => r.ClientId).Distinct().ToList();
        var authors = await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName }).ToDictionaryAsync(u => u.Id, ct);

        var employeeKeys = rows.Where(r => r.TargetType == ReviewTargetType.Employee).Select(r => r.TargetKey).Distinct().ToList();
        var employees = await db.Users.AsNoTracking().Where(u => employeeKeys.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName }).ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        var serviceIds = rows.Where(r => r.TargetType == ReviewTargetType.Service).Select(r => int.TryParse(r.TargetKey, out var i) ? i : -1).Distinct().ToList();
        var services = await db.Services.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id.ToString(), s => s.Name, ct);

        var productIds = rows.Where(r => r.TargetType == ReviewTargetType.Product).Select(r => int.TryParse(r.TargetKey, out var i) ? i : -1).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id.ToString(), p => p.Name, ct);

        string TargetName(Review r) => r.TargetType switch
        {
            ReviewTargetType.Employee => employees.GetValueOrDefault(r.TargetKey, "?"),
            ReviewTargetType.Service => services.GetValueOrDefault(r.TargetKey, "?"),
            _ => products.GetValueOrDefault(r.TargetKey, "?"),
        };

        // Other clients see "Ana A.", never a full name.
        string Author(string id) => authors.TryGetValue(id, out var a)
            ? $"{a.FirstName} {(string.IsNullOrEmpty(a.LastName) ? "" : a.LastName[..1] + ".")}".Trim()
            : "Klijent";

        return rows.Select(r => new ReviewDto(
            r.Id, r.TargetType, r.TargetKey, TargetName(r), Author(r.ClientId), r.Rating, r.Comment,
            r.CreatedAtUtc, r.IsHidden, r.ReplyText, r.ReplyAtUtc, r.ClientId == viewerId)).ToList();
    }
}
