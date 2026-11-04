using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Employees;

public class EmployeeTitles(OttomanDbContext db) : IEmployeeTitles
{
    private const string Duplicate = "Ta titula već postoji na listi.";

    public async Task<PagedResult<EmployeeTitleDto>> ListAsync(PageQuery page, CancellationToken ct)
    {
        var total = await db.EmployeeTitles.CountAsync(ct);
        var titles = await db.EmployeeTitles.AsNoTracking().OrderBy(t => t.Name).ThenBy(t => t.Id)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        var ids = titles.Select(t => t.Id).ToList();
        var used = await db.EmployeeProfiles.AsNoTracking().Where(p => ids.Contains(p.TitleId))
            .GroupBy(p => p.TitleId).Select(g => new { TitleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TitleId, x => x.Count, ct);
        var items = titles.Select(t => new EmployeeTitleDto(t.Id, t.Name, used.GetValueOrDefault(t.Id))).ToList();
        return new PagedResult<EmployeeTitleDto>(items, total);
    }

    public async Task<Result<EmployeeTitleDto>> AddAsync(string name, CancellationToken ct)
    {
        var clean = Clean(name);
        if (clean.Length == 0) return Result<EmployeeTitleDto>.Invalid("Unesite naziv titule.");
        if (await db.EmployeeTitles.AnyAsync(t => t.Name == clean, ct)) return Result<EmployeeTitleDto>.Conflict(Duplicate);

        var title = new EmployeeTitle { Name = clean };
        db.EmployeeTitles.Add(title);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            return Result<EmployeeTitleDto>.Conflict(Duplicate);
        }
        return Result<EmployeeTitleDto>.Ok(new EmployeeTitleDto(title.Id, title.Name, 0));
    }

    public async Task<Result<EmployeeTitleDto>> UpdateAsync(int id, string name, CancellationToken ct)
    {
        var clean = Clean(name);
        if (clean.Length == 0) return Result<EmployeeTitleDto>.Invalid("Unesite naziv titule.");
        var current = await db.EmployeeTitles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (current is null) return Result<EmployeeTitleDto>.NotFound("Titula nije pronađena.");
        if (await db.EmployeeTitles.AnyAsync(t => t.Id != id && t.Name == clean, ct)) return Result<EmployeeTitleDto>.Conflict(Duplicate);

        // Barbers point at the title by id, so they keep it under the new name without being touched.
        try
        {
            await db.EmployeeTitles.Where(t => t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Name, clean), ct);
        }
        catch (SqlException e) when (e.Number is 2601 or 2627)
        {
            return Result<EmployeeTitleDto>.Conflict(Duplicate);
        }

        var inUse = await db.EmployeeProfiles.CountAsync(p => p.TitleId == id, ct);
        return Result<EmployeeTitleDto>.Ok(new EmployeeTitleDto(id, clean, inUse));
    }

    public async Task<Result<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var title = await db.EmployeeTitles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (title is null) return Result<bool>.NotFound("Titula nije pronađena.");

        var inUse = await db.EmployeeProfiles.CountAsync(p => p.TitleId == id, ct);
        if (inUse > 0) return Result<bool>.Conflict(InUseMessage(title.Name, inUse));

        try
        {
            await db.EmployeeTitles.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        }
        catch (SqlException e) when (e.Number == 547)
        {
            // A barber got the title in the meantime: the foreign key refuses, and so do we.
            return Result<bool>.Conflict(InUseMessage(title.Name, await db.EmployeeProfiles.CountAsync(p => p.TitleId == id, ct)));
        }
        return Result<bool>.Ok(true);
    }

    public async Task<int> EnsureAsync(string name, CancellationToken ct)
    {
        var clean = Clean(name);
        if (clean.Length == 0) throw new ArgumentException("A title needs a name.", nameof(name));
        // The default collation is case-insensitive, so "barber" finds "Barber".
        var existing = await db.EmployeeTitles.Where(t => t.Name == clean).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        if (existing is { } id) return id;

        var title = new EmployeeTitle { Name = clean };
        db.EmployeeTitles.Add(title);
        try
        {
            await db.SaveChangesAsync(ct);
            return title.Id;
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            // Somebody added the same title a moment ago: use theirs.
            db.Entry(title).State = EntityState.Detached;
            return await db.EmployeeTitles.Where(t => t.Name == clean).Select(t => t.Id).FirstAsync(ct);
        }
    }

    private static string InUseMessage(string name, int count) =>
        $"Titulu \"{name}\" ima još {count} uposlenika. Promijenite im titulu pa je onda obrišite.";

    private static string Clean(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static bool IsUniqueViolation(DbUpdateException e) => e.InnerException is SqlException { Number: 2601 or 2627 };
}
