using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Gallery;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Images;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Gallery;

public class GalleryService(OttomanDbContext db, ImageUrlBuilder images) : IGalleryService
{
    public async Task<PagedResult<HaircutCategoryDto>> CategoriesAsync(PageQuery page, CancellationToken ct)
    {
        var total = await db.HaircutCategories.CountAsync(ct);
        var counts = await db.GalleryItems.AsNoTracking().GroupBy(i => i.CategoryId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var rows = await db.HaircutCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return new PagedResult<HaircutCategoryDto>(
            rows.Select(c => new HaircutCategoryDto(c.Id, c.Name, counts.GetValueOrDefault(c.Id))).ToList(), total);
    }

    public async Task<Result<HaircutCategoryDto>> CreateCategoryAsync(HaircutCategoryRequest r, CancellationToken ct)
    {
        var name = r.Name.Trim();
        if (await db.HaircutCategories.AnyAsync(c => c.Name == name, ct))
            return Result<HaircutCategoryDto>.Conflict("Kategorija s tim nazivom već postoji.");
        var next = (await db.HaircutCategories.MaxAsync(c => (int?)c.SortOrder, ct) ?? -1) + 1;
        var category = new HaircutCategory { Name = name, SortOrder = next };
        db.HaircutCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return Result<HaircutCategoryDto>.Ok(new HaircutCategoryDto(category.Id, category.Name, 0));
    }

    public async Task<Result<HaircutCategoryDto>> RenameCategoryAsync(int id, HaircutCategoryRequest r, CancellationToken ct)
    {
        var category = await db.HaircutCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null) return Result<HaircutCategoryDto>.NotFound();
        var name = r.Name.Trim();
        if (await db.HaircutCategories.AnyAsync(c => c.Name == name && c.Id != id, ct))
            return Result<HaircutCategoryDto>.Conflict("Kategorija s tim nazivom već postoji.");
        category.Name = name;
        await db.SaveChangesAsync(ct);
        var count = await db.GalleryItems.CountAsync(i => i.CategoryId == id, ct);
        return Result<HaircutCategoryDto>.Ok(new HaircutCategoryDto(category.Id, category.Name, count));
    }

    public async Task<Result<bool>> DeleteCategoryAsync(int id, CancellationToken ct)
    {
        if (!await db.HaircutCategories.AnyAsync(c => c.Id == id, ct)) return Result<bool>.NotFound();
        if (await db.GalleryItems.AnyAsync(i => i.CategoryId == id, ct))
            return Result<bool>.Conflict("Kategorija se koristi u galeriji. Prvo premjestite ili obrišite te radove.");
        await db.HaircutCategories.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<PagedResult<GalleryItemDto>> PageAsync(
        int? categoryId, bool? featured, string? employeeId, PageQuery page, CancellationToken ct)
    {
        var query = db.GalleryItems.AsNoTracking().AsQueryable();
        if (categoryId is { } c) query = query.Where(i => i.CategoryId == c);
        if (featured is { } f) query = query.Where(i => i.IsFeatured == f);
        if (!string.IsNullOrEmpty(employeeId)) query = query.Where(i => i.EmployeeId == employeeId);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(i => i.CreatedAtUtc).ThenByDescending(i => i.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(i => new Row(i.Id, i.ImageKey, i.CategoryId, i.EmployeeId, i.Caption, i.IsFeatured, i.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<GalleryItemDto>(await ToDtosAsync(rows, ct), total);
    }

    public async Task<Result<GalleryItemDto>> GetAsync(int id, CancellationToken ct)
    {
        var row = await db.GalleryItems.AsNoTracking().Where(i => i.Id == id)
            .Select(i => new Row(i.Id, i.ImageKey, i.CategoryId, i.EmployeeId, i.Caption, i.IsFeatured, i.CreatedAtUtc))
            .FirstOrDefaultAsync(ct);
        return row is null ? Result<GalleryItemDto>.NotFound() : Result<GalleryItemDto>.Ok((await ToDtosAsync([row], ct))[0]);
    }

    public async Task<Result<GalleryItemDto>> UpdateAsync(int id, UpdateGalleryItemRequest r, CancellationToken ct)
    {
        var item = await db.GalleryItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return Result<GalleryItemDto>.NotFound();
        if (!await db.HaircutCategories.AnyAsync(c => c.Id == r.CategoryId, ct))
            return Result<GalleryItemDto>.Invalid("Odabrana kategorija frizure ne postoji.");
        var employeeId = string.IsNullOrWhiteSpace(r.EmployeeId) ? null : r.EmployeeId;
        if (employeeId is not null && !await db.EmployeeProfiles.AnyAsync(p => p.UserId == employeeId, ct))
            return Result<GalleryItemDto>.Invalid("Odabrani uposlenik ne postoji.");

        item.CategoryId = r.CategoryId;
        item.EmployeeId = employeeId;
        item.Caption = string.IsNullOrWhiteSpace(r.Caption) ? null : r.Caption.Trim();
        item.IsFeatured = r.IsFeatured;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private sealed record Row(int Id, string ImageKey, int CategoryId, string? EmployeeId, string? Caption, bool IsFeatured, DateTime CreatedAtUtc);

    private async Task<List<GalleryItemDto>> ToDtosAsync(List<Row> rows, CancellationToken ct)
    {
        var categoryIds = rows.Select(r => r.CategoryId).Distinct().ToList();
        var categories = await db.HaircutCategories.AsNoTracking().Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var employeeIds = rows.Select(r => r.EmployeeId).Where(e => e != null).Distinct().ToList();
        var employees = await db.Users.AsNoTracking().Where(u => employeeIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName }).ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        return rows.Select(r => new GalleryItemDto(
            r.Id, images.Url(r.ImageKey) ?? "", r.CategoryId, categories.GetValueOrDefault(r.CategoryId, ""),
            r.EmployeeId, r.EmployeeId is null ? null : employees.GetValueOrDefault(r.EmployeeId),
            r.Caption, r.IsFeatured, r.CreatedAtUtc)).ToList();
    }
}
