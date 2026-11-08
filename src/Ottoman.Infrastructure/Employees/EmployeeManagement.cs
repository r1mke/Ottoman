using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Accounts;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Application.Images;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Identity;
using Ottoman.Infrastructure.Images;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Employees;

public class EmployeeManagement(
    OttomanDbContext db, UserManager<AppUser> users, TimeProvider clock, ImageUrlBuilder images, IEmployeeTitles titles,
    IOutbox outbox) : IEmployeeService
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
            select new { Profile = p, User = u, TitleName = p.Title.Name };
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => (x.User.FirstName + " " + x.User.LastName).Contains(term) || x.TitleName.Contains(term));
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
            x.Profile.UserId, x.User.FirstName, x.User.LastName, x.User.FullName, x.TitleName, x.Profile.Bio,
            images.AvatarUrl(x.Profile.PhotoKey, x.Profile.PhotoZoom), x.Profile.IsActive, byEmployee[x.Profile.UserId].OrderBy(s => s).ToList(),
            includePrivate ? x.User.Email : null, includePrivate ? x.User.PhoneNumber : null,
            x.Profile.Gender?.ToString(), x.Profile.PhotoZoom, x.Profile.TitleId, LockedUntil(x.User, includePrivate))).ToList();
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
        var profiles = await db.EmployeeProfiles.AsNoTracking().Include(p => p.Services).Include(p => p.Title)
            .Where(filter).ToListAsync(ct);
        var ids = profiles.Select(p => p.UserId).ToList();
        var byId = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        return profiles
            .Where(p => byId.ContainsKey(p.UserId))
            .Select(p => (p, byId[p.UserId]))
            .OrderBy(x => x.Item2.FirstName).ThenBy(x => x.Item2.LastName)
            .ToList();
    }

    private EmployeeDto ToDto(EmployeeProfile p, AppUser u, bool includePrivate) => new(
        p.UserId, u.FirstName, u.LastName, u.FullName, p.Title.Name, p.Bio, images.AvatarUrl(p.PhotoKey, p.PhotoZoom), p.IsActive,
        p.Services.Select(s => s.ServiceId).OrderBy(x => x).ToList(),
        includePrivate ? u.Email : null,
        includePrivate ? u.PhoneNumber : null,
        p.Gender?.ToString(),
        p.PhotoZoom,
        p.TitleId,
        LockedUntil(u, includePrivate));

    /// <summary>Only the admin sees whether a barber is locked out of signing in.</summary>
    private DateTime? LockedUntil(AppUser u, bool includePrivate) =>
        includePrivate ? AccountLock.LockedUntil(u.LockoutEnabled, u.LockoutEnd, clock.GetUtcNow().UtcDateTime) : null;

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

        // A title typed through the API joins the pick list; the barber points at it by id.
        var titleId = await titles.EnsureAsync(r.Title, ct);
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
                TitleId = titleId,
                Gender = GenderNames.Parse(r.Gender),
                Bio = r.Bio?.Trim(),
                Services = serviceIds.Select(id => new EmployeeService { EmployeeId = user.Id, ServiceId = id }).ToList(),
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        if (failure is not null) return Result<EmployeeDto>.Invalid(failure);
        return await GetAsync(user.Id, true, ct);
    }

    public async Task<Result<EmployeeDto>> UpdateAsync(string id, UpdateEmployeeRequest r, CancellationToken ct)
    {
        if (!await db.EmployeeProfiles.AnyAsync(p => p.UserId == id, ct)) return Result<EmployeeDto>.NotFound();
        // Resolved before anything is tracked: a title typed through the API joins the pick list.
        var titleId = await titles.EnsureAsync(r.Title, ct);
        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.UserId == id, ct);
        var user = await users.FindByIdAsync(id);
        if (profile is null || user is null) return Result<EmployeeDto>.NotFound();

        user.FirstName = r.FirstName.Trim();
        user.LastName = r.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim();
        profile.TitleId = titleId;
        if (r.Gender is not null) profile.Gender = GenderNames.Parse(r.Gender);
        if (r.PhotoZoom is { } zoom) profile.PhotoZoom = zoom;
        profile.Bio = r.Bio?.Trim();
        profile.IsActive = r.IsActive;

        await users.UpdateAsync(user);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, true, ct);
    }

    public async Task<Result<bool>> UpdateOwnProfileAsync(string userId, UpdateOwnEmployeeProfileRequest r, CancellationToken ct)
    {
        var bio = string.IsNullOrWhiteSpace(r.Bio) ? null : r.Bio.Trim();
        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null)
        {
            if (r.Gender is not null) profile.Gender = GenderNames.Parse(r.Gender);
            if (r.PhotoZoom is { } zoom) profile.PhotoZoom = zoom;
            profile.Bio = bio;
        }
        else
        {
            // No barber profile: the admin's gender and bio live on the account (the route is staff only).
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (user is null) return Result<bool>.NotFound("Profil nije pronađen.");
            if (r.Gender is not null) user.Gender = GenderNames.Parse(r.Gender);
            user.Bio = bio;
        }

        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
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

    public async Task<TimeOffOutcome> AddTimeOffAsync(string id, CreateTimeOffRequest r, CancellationToken ct)
    {
        if (!await db.EmployeeProfiles.AnyAsync(p => p.UserId == id, ct))
            return new TimeOffOutcome(Result<TimeOffDto>.NotFound(), []);

        var start = DateTime.SpecifyKind(r.StartUtc, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(r.EndUtc, DateTimeKind.Utc);
        var clashes = await ClashesAsync(id, start, end, ct);
        if (clashes.Count > 0 && !r.Force)
        {
            var listed = clashes.Select(c => c.Dto).ToList();
            return new TimeOffOutcome(Result<TimeOffDto>.Conflict(TimeOffClashes.Message(listed)), listed);
        }

        var t = new TimeOff { EmployeeId = id, StartUtc = start, EndUtc = end, Reason = r.Reason?.Trim() };
        db.TimeOff.Add(t);
        // Forced over bookings: they stay (the admin moves or cancels them), and each client hears about it in the same
        // transaction as the time off, through the outbox.
        foreach (var c in clashes)
            outbox.Enqueue(new ReservationAffectedByTimeOff(c.ReservationId, c.ClientId, id, c.StartUtc));
        await db.SaveChangesAsync(ct);
        return new TimeOffOutcome(
            Result<TimeOffDto>.Ok(new TimeOffDto(t.Id, t.StartUtc, t.EndUtc, t.Reason)),
            clashes.Select(c => c.Dto).ToList());
    }

    private sealed record Clash(int ReservationId, string ClientId, DateTime StartUtc, TimeOffClashDto Dto);

    /// <summary>The barber's pending or confirmed bookings that overlap [start, end).</summary>
    private async Task<IReadOnlyList<Clash>> ClashesAsync(string employeeId, DateTime start, DateTime end, CancellationToken ct)
    {
        var rows = await db.Reservations.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId
                        && (x.Status == ReservationStatus.Pending || x.Status == ReservationStatus.Confirmed)
                        && x.StartUtc < end && x.EndUtc > start)
            .OrderBy(x => x.StartUtc)
            .Join(db.Users, x => x.ClientId, u => u.Id,
                (x, u) => new { x.Id, x.ClientId, x.StartUtc, x.EndUtc, u.FirstName, u.LastName })
            .ToListAsync(ct);
        if (rows.Count == 0) return [];

        var zoneId = await db.SalonSettings.AsNoTracking().Select(s => s.TimeZoneId).FirstAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        return rows.Select(x => new Clash(x.Id, x.ClientId, x.StartUtc, new TimeOffClashDto(
            x.Id, $"{x.FirstName} {x.LastName}".Trim(), x.StartUtc, x.EndUtc,
            LocalTime.ToLocal(x.StartUtc, zone).ToString("dd.MM.yyyy. HH:mm", System.Globalization.CultureInfo.InvariantCulture))))
            .ToList();
    }

    public async Task<Result<bool>> RemoveTimeOffAsync(string id, int timeOffId, CancellationToken ct)
    {
        // Scoped by employee so one employee's id can never delete another's time off.
        var removed = await db.TimeOff.Where(t => t.Id == timeOffId && t.EmployeeId == id).ExecuteDeleteAsync(ct);
        return removed == 0 ? Result<bool>.NotFound() : Result<bool>.Ok(true);
    }
}
