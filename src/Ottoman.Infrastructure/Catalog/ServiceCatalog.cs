using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Catalog;
using Ottoman.Application.Common;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Catalog;

public class ServiceCatalog(OttomanDbContext db) : IServiceCatalog
{
    public async Task<IReadOnlyList<ServiceDto>> ListAsync(string? employeeId, bool includeInactive, CancellationToken ct)
    {
        var query = db.Services.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(s => s.IsActive);
        if (employeeId is not null)
            query = query.Where(s => db.EmployeeServices.Any(es => es.ServiceId == s.Id && es.EmployeeId == employeeId));

        return await query.OrderBy(s => s.Name).Select(s => ToDto(s)).ToListAsync(ct);
    }

    public async Task<Result<ServiceDto>> GetAsync(int id, CancellationToken ct)
    {
        var s = await db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? Result<ServiceDto>.NotFound() : Result<ServiceDto>.Ok(ToDto(s));
    }

    public async Task<Result<ServiceDto>> CreateAsync(ServiceUpsertRequest r, CancellationToken ct)
    {
        var name = r.Name.Trim();
        if (await db.Services.AnyAsync(s => s.Name == name, ct))
            return Result<ServiceDto>.Conflict("Usluga s tim nazivom već postoji.");

        var s = new Service
        {
            Name = name,
            Description = r.Description?.Trim(),
            Price = r.Price,
            DurationMinutes = r.DurationMinutes,
            IsActive = r.IsActive,
        };
        db.Services.Add(s);
        await db.SaveChangesAsync(ct);
        return Result<ServiceDto>.Ok(ToDto(s));
    }

    public async Task<Result<ServiceDto>> UpdateAsync(int id, ServiceUpsertRequest r, CancellationToken ct)
    {
        var s = await db.Services.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result<ServiceDto>.NotFound();

        var name = r.Name.Trim();
        if (await db.Services.AnyAsync(x => x.Id != id && x.Name == name, ct))
            return Result<ServiceDto>.Conflict("Usluga s tim nazivom već postoji.");

        // Price/duration changes affect new bookings only: reservations store their own snapshot.
        s.Name = name;
        s.Description = r.Description?.Trim();
        s.Price = r.Price;
        s.DurationMinutes = r.DurationMinutes;
        s.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        return Result<ServiceDto>.Ok(ToDto(s));
    }

    public async Task<Result<bool>> DeactivateAsync(int id, CancellationToken ct)
    {
        var s = await db.Services.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return Result<bool>.NotFound();
        s.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private static ServiceDto ToDto(Service s) =>
        new(s.Id, s.Name, s.Description, s.Price, s.DurationMinutes, s.ImageKey, s.IsActive);
}
