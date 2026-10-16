using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Application.Images;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Identity;
using Ottoman.Infrastructure.Images;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Employees;

public class EmployeeManagement(OttomanDbContext db, UserManager<AppUser> users, TimeProvider clock, ImageUrlBuilder images) : IEmployeeService
{
    // ------------------------------------------------------------------ read

    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(bool includeInactive, bool includePrivate, CancellationToken ct)
    {
        var rows = await LoadAsync(p => includeInactive || p.IsActive, ct);
        return rows.Select(x => ToDto(x.Profile, x.User, includePrivate)).ToList();
    }

    public async Task<PagedResult<EmployeeDto>> PageAsync(
        bool includeInactive, bool includePrivate, string? search, PageQuery page, CancellationToken ct)
    {
        var query =
            from p in db.EmployeeProfiles.AsNoTracking()
            join u in db.Users.AsNoTracking() on p.UserId equals u.Id
            where includeInactive || p.IsActive
            select new { Profile = p, User = u };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => (x.User.FirstName + " " + x.User.LastName).Contains(term) || x.Profile.Title.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.User.FirstName).ThenBy(x => x.User.LastName).ThenBy(x => x.User.Id)
            .Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);

        // One more query for the service ids of just these barbers, not one per barber.
        var ids = rows.Select(x => x.Profile.UserId).ToList();
        var services = await db.EmployeeServices.AsNoTracking().Where(s => ids.Contains(s.EmployeeId))
            .Select(s => new { s.EmployeeId, s.ServiceId }).ToListAsync(ct);
        var byEmployee = services.ToLookup(s => s.EmployeeId, s => s.ServiceId);

        var items = rows.Select(x => new EmployeeDto(
            x.Profile.UserId, x.User.FirstName, x.User.LastName, x.User.FullName, x.Profile.Title, x.Profile.Bio,
            images.Url(x.Profile.PhotoKey), x.Profile.IsActive, byEmployee[x.Profile.UserId].OrderBy(s => s).ToList(),
            includePrivate ? x.User.Email : null, includePrivate ? x.User.PhoneNumber : null)).ToList();
        return new PagedResult<EmployeeDto>(items, total);
    }

    public async Task<Result<EmployeeDto>> GetAsync(string id, bool includePrivate, CancellationToken ct)
    {
        var rows = await LoadAsync(p => p.UserId == id && (includePrivate || p.IsActive), ct);
        return rows.Count == 0
            ? Result<EmployeeDto>.NotFound()
            : Result<EmployeeDto>.Ok(ToDto(rows[0].Profile, rows[0].User, includePrivate));
    }

    private async Task<List<(EmployeeProfile Profile, AppUser User)>> LoadAsync(
        System.Linq.Expressions.Expression<Func<EmployeeProfile, bool>> filter, CancellationToken ct)
    {
        var profiles = await db.EmployeeProfiles.AsNoTracking().Include(p => p.Services).Where(filter).ToListAsync(ct);
        var ids = profiles.Select(p => p.UserId).ToList();
        var byId = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        return profiles
            .Where(p => byId.ContainsKey(p.UserId))
            .Select(p => (p, byId[p.UserId]))
            .OrderBy(x => x.Item2.FirstName).ThenBy(x => x.Item2.LastName)
            .ToList();
    }

    private EmployeeDto ToDto(EmployeeProfile p, AppUser u, bool includePrivate) => new(
        p.UserId, u.FirstName, u.LastName, u.FullName, p.Title, p.Bio, images.Url(p.PhotoKey), p.IsActive,
        p.Services.Select(s => s.ServiceId).OrderBy(x => x).ToList(),
        includePrivate ? u.Email : null,
        includePrivate ? u.PhoneNumber : null);

    // ----------------------------------------------------------------- write

    public async Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeRequest r, CancellationToken ct)
    {
        var email = r.Email.Trim();
        if (await users.FindByEmailAsync(email) is not null)
            return Result<EmployeeDto>.Conflict("Račun s tom email adresom već postoji.");

        var serviceIds = r.ServiceIds?.Distinct().ToList() ?? [];
        if (!await ServicesExist(serviceIds, ct))
            return Result<EmployeeDto>.Invalid("Jedna ili više odabranih usluga ne postoji.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = r.FirstName.Trim(),
            LastName = r.LastName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim(),
            RegisteredAtUtc = clock.GetUtcNow().UtcDateTime,
        };

        string? failure = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var created = await users.CreateAsync(user, r.Password);
            if (!created.Succeeded)
            {
                failure = string.Join(" ", created.Errors.Select(e => e.Description));
                return;
            }
            await users.AddToRoleAsync(user, Roles.Employee);
            db.EmployeeProfiles.Add(new EmployeeProfile
            {
                UserId = user.Id,
                Title = r.Title.Trim(),
                Bio = r.Bio?.Trim(),
                Services = serviceIds.Select(id => new EmployeeService { EmployeeId = user.Id, ServiceId = id }).ToList(),
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return failure is not null ? Result<EmployeeDto>.Invalid(failure) : await GetAsync(user.Id, true, ct);
    }

    public async Task<Result<EmployeeDto>> UpdateAsync(string id, UpdateEmployeeRequest r, CancellationToken ct)
    {
        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.UserId == id, ct);
        var user = await users.FindByIdAsync(id);
        if (profile is null || user is null) return Result<EmployeeDto>.NotFound();

        user.FirstName = r.FirstName.Trim();
        user.LastName = r.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim();
        profile.Title = r.Title.Trim();
        profile.Bio = r.Bio?.Trim();
        profile.IsActive = r.IsActive;

        await users.UpdateAsync(user);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, true, ct);
    }

    public async Task<Result<EmployeeDto>> SetServicesAsync(string id, IReadOnlyList<int> serviceIds, CancellationToken ct)
    {
        var profile = await db.EmployeeProfiles.Include(p => p.Services).FirstOrDefaultAsync(p => p.UserId == id, ct);
        if (profile is null) return Result<EmployeeDto>.NotFound();

        var wanted = serviceIds.Distinct().ToList();
        if (!await ServicesExist(wanted, ct))
            return Result<EmployeeDto>.Invalid("Jedna ili više odabranih usluga ne postoji.");

        // Existing reservations keep their own snapshot, so un-assigning a service is always safe.
        profile.Services.RemoveAll(s => !wanted.Contains(s.ServiceId));
        foreach (var sid in wanted.Where(w => profile.Services.All(s => s.ServiceId != w)))
            profile.Services.Add(new EmployeeService { EmployeeId = id, ServiceId = sid });

        await db.SaveChangesAsync(ct);
        return await GetAsync(id, true, ct);
    }

    private async Task<bool> ServicesExist(List<int> ids, CancellationToken ct) =>
        ids.Count == 0 || await db.Services.CountAsync(s => ids.Contains(s.Id), ct) == ids.Count;

    // ---------------------------------------------------------- working hours

    public async Task<Result<IReadOnlyList<WorkingHoursDto>>> GetWorkingHoursAsync(string id, CancellationToken ct)
    {
        if (!await db.EmployeeProfiles.AnyAsync(p => p.UserId == id, ct))
            return Result<IReadOnlyList<WorkingHoursDto>>.NotFound();

        var rows = await db.WorkingHours.AsNoTracking().Where(w => w.EmployeeId == id).OrderBy(w => w.DayOfWeek).ToListAsync(ct);
        return Result<IReadOnlyList<WorkingHoursDto>>.Ok(rows.Select(ToDto).ToList());
    }

    public async Task<Result<IReadOnlyList<WorkingHoursDto>>> SetWorkingHoursAsync(
        string id, SetWorkingHoursRequest request, CancellationToken ct)
    {
        var profile = await db.EmployeeProfiles.Include(p => p.WorkingHours).FirstOrDefaultAsync(p => p.UserId == id, ct);
        if (profile is null) return Result<IReadOnlyList<WorkingHoursDto>>.NotFound();

        // Replaces the whole week: days that are absent from the request are days off.
        profile.WorkingHours.Clear();
        foreach (var d in request.Days)
        {
            HhMm.TryParse(d.Start, out var start);
            HhMm.TryParse(d.End, out var end);
            TimeOnly? bs = HhMm.TryParse(d.BreakStart, out var b1) ? b1 : null;
            TimeOnly? be = HhMm.TryParse(d.BreakEnd, out var b2) ? b2 : null;
            profile.WorkingHours.Add(new WorkingHours
            {
                EmployeeId = id,
                DayOfWeek = d.DayOfWeek,
                Start = start,
                End = end,
                BreakStart = bs,
                BreakEnd = be,
            });
        }
        await db.SaveChangesAsync(ct);
        return await GetWorkingHoursAsync(id, ct);
    }

    private static WorkingHoursDto ToDto(WorkingHours w) => new(
        w.DayOfWeek, HhMm.Format(w.Start), HhMm.Format(w.End),
        w.BreakStart is { } bs ? HhMm.Format(bs) : null,
        w.BreakEnd is { } be ? HhMm.Format(be) : null);

    // --------------------------------------------------------------- time off

    public async Task<Result<IReadOnlyList<TimeOffDto>>> ListTimeOffAsync(string id, CancellationToken ct)
    {
        if (!await db.EmployeeProfiles.AnyAsync(p => p.UserId == id, ct))
            return Result<IReadOnlyList<TimeOffDto>>.NotFound();

        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await db.TimeOff.AsNoTracking()
            .Where(t => t.EmployeeId == id && t.EndUtc > now)
            .OrderBy(t => t.StartUtc).ToListAsync(ct);
        return Result<IReadOnlyList<TimeOffDto>>.Ok(rows.Select(t => new TimeOffDto(t.Id, t.StartUtc, t.EndUtc, t.Reason)).ToList());
    }

    public async Task<Result<PagedResult<TimeOffDto>>> PageTimeOffAsync(string id, PageQuery page, CancellationToken ct)
    {
        if (!await db.EmployeeProfiles.AnyAsync(p => p.UserId == id, ct))
            return Result<PagedResult<TimeOffDto>>.NotFound();

        var now = clock.GetUtcNow().UtcDateTime;
        var query = db.TimeOff.AsNoTracking().Where(t => t.EmployeeId == id && t.EndUtc > now);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(t => t.StartUtc).ThenBy(t => t.Id).Skip(page.Skip).Take(page.SafePageSize).ToListAsync(ct);
        return Result<PagedResult<TimeOffDto>>.Ok(
            new PagedResult<TimeOffDto>(rows.Select(t => new TimeOffDto(t.Id, t.StartUtc, t.EndUtc, t.Reason)).ToList(), total));
    }

    public async Task<Result<TimeOffDto>> AddTimeOffAsync(string id, CreateTimeOffRequest r, CancellationToken ct)
    {
        if (!await db.EmployeeProfiles.AnyAsync(p => p.UserId == id, ct))
            return Result<TimeOffDto>.NotFound();

        var t = new TimeOff
        {
            EmployeeId = id,
            StartUtc = DateTime.SpecifyKind(r.StartUtc, DateTimeKind.Utc),
            EndUtc = DateTime.SpecifyKind(r.EndUtc, DateTimeKind.Utc),
            Reason = r.Reason?.Trim(),
        };
        db.TimeOff.Add(t);
        await db.SaveChangesAsync(ct);
        return Result<TimeOffDto>.Ok(new TimeOffDto(t.Id, t.StartUtc, t.EndUtc, t.Reason));
    }

    public async Task<Result<bool>> RemoveTimeOffAsync(string id, int timeOffId, CancellationToken ct)
    {
        // Scoped by employee so one employee's id can never delete another's time off.
        var removed = await db.TimeOff.Where(t => t.Id == timeOffId && t.EmployeeId == id).ExecuteDeleteAsync(ct);
        return removed == 0 ? Result<bool>.NotFound() : Result<bool>.Ok(true);
    }
}
