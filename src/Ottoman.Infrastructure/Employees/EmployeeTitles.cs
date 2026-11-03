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
        var used = await db.EmployeeProfiles.AsNoTracking()
            .GroupBy(p => p.Title).Select(g => new { Title = g.Key, Count = g.Count() }).ToListAsync(ct);
        var items = titles
            .Select(t => new EmployeeTitleDto(
                t.Id, t.Name,
                used.Where(u => string.Equals(u.Title, t.Name, StringComparison.OrdinalIgnoreCase)).Sum(u => u.Count)))
            .ToList();
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

    public async Task<Result<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var title = await db.EmployeeTitles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (title is null) return Result<bool>.NotFound("Titula nije pronađena.");

        var inUse = await db.EmployeeProfiles.CountAsync(p => p.Title == title.Name, ct);
        if (inUse > 0)
            return Result<bool>.Conflict($"Titulu \"{title.Name}\" ima još {inUse} uposlenika. Promijenite im titulu pa je onda obrišite.");

        await db.EmployeeTitles.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task EnsureAsync(string name, CancellationToken ct)
    {
        var clean = Clean(name);
        if (clean.Length == 0 || await db.EmployeeTitles.AnyAsync(t => t.Name == clean, ct)) return;
        db.EmployeeTitles.Add(new EmployeeTitle { Name = clean });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            // Somebody added the same title a moment ago: that is the outcome we wanted.
            db.ChangeTracker.Clear();
        }
    }

    private static string Clean(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static bool IsUniqueViolation(DbUpdateException e) => e.InnerException is SqlException { Number: 2601 or 2627 };
}
