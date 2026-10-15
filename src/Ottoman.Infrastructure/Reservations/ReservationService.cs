using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Reservations;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Reservations;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Scheduling;

namespace Ottoman.Infrastructure.Reservations;

public class ReservationService(
    OttomanDbContext db,
    AvailabilityLoader loader,
    IOutbox outbox,
    TimeProvider clock) : IReservationService
{
    private const string TakenMessage = "Termin je upravo zauzet. Osvježite termine i odaberite drugi.";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- create

    public async Task<Result<ReservationDto>> CreateAsync(Actor actor, CreateReservationRequest r, CancellationToken ct)
    {
        var now = Now;

        // Who is the appointment for? Clients book for themselves; admins may book for a client (walk-in).
        string clientId;
        if (actor.IsClient)
        {
            clientId = actor.UserId;
        }
        else if (actor.IsAdmin && !string.IsNullOrWhiteSpace(r.ClientId))
        {
            if (!await IsClientAsync(r.ClientId, ct))
                return Result<ReservationDto>.Invalid("Odabrani klijent ne postoji.");
            clientId = r.ClientId;
        }
        else
        {
            return Result<ReservationDto>.Invalid("Navedite klijenta za kojeg se zakazuje termin.");
        }

        var settings = await db.SalonSettings.AsNoTracking().SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var localDate = DateOnly.FromDateTime(LocalTime.ToLocal(r.StartUtc, zone));

        var loaded = await loader.LoadAsync(r.EmployeeId, localDate, r.ServiceIds, excludeReservationId: null, ct);
        if (!loaded.Succeeded) return new Result<ReservationDto>(loaded.Status, Error: loaded.Error);
        var (query, chosen, _, _) = loaded.Value!;

        // Salon staff may book at short notice; clients must respect the lead time.
        if (!actor.IsClient) query = query with { MinLeadMinutes = 0 };

        var activeCount = await db.Reservations.CountAsync(x =>
            x.ClientId == clientId && x.EndUtc > now &&
            (x.Status == ReservationStatus.Confirmed || x.Status == ReservationStatus.Pending), ct);
        var violations = BookingPolicy.Validate(r.StartUtc, now, chosen.Count, activeCount, r.Note, bypassClientLimit: !actor.IsClient);
        if (violations.Count > 0) return Result<ReservationDto>.Invalid(string.Join(" ", violations));

        // The server never trusts the client's idea of what is free: re-derive it and require an exact match.
        if (!SlotCalculator.AllStarts(query).Contains(r.StartUtc))
            return Result<ReservationDto>.Conflict("Odabrani termin nije dostupan. Osvježite termine i odaberite drugi.");

        var duration = query.DurationMinutes;
        var reservation = new Reservation
        {
            ClientId = clientId,
            EmployeeId = r.EmployeeId,
            StartUtc = r.StartUtc,
            EndUtc = r.StartUtc.AddMinutes(duration),
            Status = ReservationStatus.Confirmed, // no approval step: a free slot is booked immediately
            Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim(),
            TotalPrice = chosen.Sum(s => s.Price),
            TotalDurationMinutes = duration,
            CreatedAtUtc = now,
            Items = chosen.Select(s => new ReservationItem
            {
                ServiceId = s.Id,
                ServiceName = s.Name,
                Price = s.Price,
                DurationMinutes = s.DurationMinutes,
            }).ToList(),
            Slots = SlotGrid.Occupied(r.StartUtc, duration, settings.SlotMinutes)
                .Select(t => new ReservationSlot { EmployeeId = r.EmployeeId, SlotStartUtc = t }).ToList(),
        };

        db.Reservations.Add(reservation);

        var taken = false;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            taken = false;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await db.SaveChangesAsync(ct); // assigns the id; the unique (employee, step) index decides races
                outbox.Enqueue(new ReservationCreated(
                    reservation.Id, clientId, r.EmployeeId, reservation.StartUtc, reservation.EndUtc, reservation.TotalPrice));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException e) when (IsUniqueViolation(e))
            {
                // A concurrent booking won the race for one of the steps: exactly one request can succeed.
                taken = true;
                await tx.RollbackAsync(ct);
            }
        });

        return taken ? Result<ReservationDto>.Conflict(TakenMessage) : await GetAsync(actor, reservation.Id, ct);
    }

    // ------------------------------------------------------------------ read

    public async Task<Result<ReservationDto>> GetAsync(Actor actor, int id, CancellationToken ct)
    {
        var res = await db.Reservations.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (res is null || !CanSee(actor, res)) return Result<ReservationDto>.NotFound("Rezervacija nije pronađena.");

        var dto = (await ToDtosAsync(actor, [res], ct))[0];
        return Result<ReservationDto>.Ok(dto);
    }

    public async Task<PagedResult<ReservationDto>> ListAsync(Actor actor, ReservationFilter f, PageQuery page, CancellationToken ct)
    {
        var now = Now;
        var query = db.Reservations.AsNoTracking().AsQueryable();

        // Scope first: it is what keeps one user from listing another's reservations.
        query = actor.Kind switch
        {
            ActorKind.Client => query.Where(r => r.ClientId == actor.UserId),
            ActorKind.Employee => query.Where(r => r.EmployeeId == actor.UserId),
            _ => query,
        };

        if (actor.IsAdmin || actor.IsEmployee)
        {
            if (f.EmployeeId is not null && actor.IsAdmin) query = query.Where(r => r.EmployeeId == f.EmployeeId);
            if (f.ClientId is not null) query = query.Where(r => r.ClientId == f.ClientId);
        }

        var (fromUtc, toUtc) = await ResolveRangeAsync(f.FromDate, f.ToDate, f.FromUtc, f.ToUtc, ct);
        if (fromUtc is { } from) query = query.Where(r => r.StartUtc >= from);
        if (toUtc is { } to) query = query.Where(r => r.StartUtc < to);
        if (f.Status is { } status) query = query.Where(r => r.Status == status);

        if (f.Upcoming == true)
            query = query.Where(r => r.EndUtc > now && (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Pending));
        else if (f.Upcoming == false)
            query = query.Where(r => !(r.EndUtc > now && (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Pending)));

        var total = await query.CountAsync(ct);
        var ordered = f.Upcoming == true
            ? query.OrderBy(r => r.StartUtc).ThenBy(r => r.Id)
            : query.OrderByDescending(r => r.StartUtc).ThenByDescending(r => r.Id);

        var rows = await ordered.Skip(page.Skip).Take(page.SafePageSize).Include(r => r.Items).ToListAsync(ct);
        return new PagedResult<ReservationDto>(await ToDtosAsync(actor, rows, ct), total);
    }

    public async Task<ReservationSummaryDto> SummaryAsync(Actor actor, DateOnly? fromDate, DateOnly? toDate, string? employeeId, CancellationToken ct)
    {
        var now = Now;
        var (fromUtc, toUtc) = await ResolveRangeAsync(fromDate, toDate, null, null, ct);
        var query = db.Reservations.AsNoTracking().AsQueryable();
        if (actor.IsEmployee) query = query.Where(r => r.EmployeeId == actor.UserId);
        else if (actor.IsAdmin && employeeId is not null) query = query.Where(r => r.EmployeeId == employeeId);
        else if (actor.IsClient) query = query.Where(r => r.ClientId == actor.UserId);

        if (fromUtc is { } from) query = query.Where(r => r.StartUtc >= from);
        if (toUtc is { } to) query = query.Where(r => r.StartUtc < to);

        var byStatus = await query.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(ReservationStatus s) => byStatus.Where(x => x.Status == s).Sum(x => x.Count);

        var upcoming = await query.CountAsync(r => r.EndUtc > now &&
            (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Pending), ct);
        var clients = await query.Select(r => r.ClientId).Distinct().CountAsync(ct);

        return new ReservationSummaryDto(upcoming, Count(ReservationStatus.Completed), Count(ReservationStatus.Cancelled), Count(ReservationStatus.NoShow), clients);
    }

    // ---------------------------------------------------------- state changes

    public Task<Result<ReservationDto>> CancelAsync(Actor actor, int id, string? reason, CancellationToken ct) =>
        ChangeStatusAsync(actor, id, ReservationStatus.Cancelled, reason, ct);

    public Task<Result<ReservationDto>> CompleteAsync(Actor actor, int id, CancellationToken ct) =>
        ChangeStatusAsync(actor, id, ReservationStatus.Completed, null, ct);

    public Task<Result<ReservationDto>> MarkNoShowAsync(Actor actor, int id, CancellationToken ct) =>
        ChangeStatusAsync(actor, id, ReservationStatus.NoShow, null, ct);

    private async Task<Result<ReservationDto>> ChangeStatusAsync(
        Actor actor, int id, ReservationStatus target, string? reason, CancellationToken ct)
    {
        var res = await db.Reservations.Include(r => r.Slots).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (res is null || !CanSee(actor, res)) return Result<ReservationDto>.NotFound("Rezervacija nije pronađena.");

        var settings = await db.SalonSettings.AsNoTracking().SingleAsync(ct);
        var now = Now;
        var decision = ReservationStateMachine.Can(res.Status, target, actor.Kind, now, res.StartUtc, settings.CancelWindowMinutes);
        if (!decision.Allowed) return Result<ReservationDto>.Conflict(decision.Reason!);

        res.Status = target;
        if (target == ReservationStatus.Cancelled)
        {
            res.CancelledAtUtc = now;
            res.CancelledBy = actor.Kind.ToString();
            res.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            db.ReservationSlots.RemoveRange(res.Slots); // frees the time for other clients
            outbox.Enqueue(new ReservationCancelled(res.Id, res.ClientId, res.EmployeeId, res.StartUtc, res.CancelledBy, res.CancelReason));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ReservationDto>.Conflict("Rezervacija je u međuvremenu promijenjena. Osvježite i pokušajte ponovo.");
        }

        return await GetAsync(actor, id, ct);
    }

    public async Task<Result<ReservationDto>> RescheduleAsync(
        Actor actor, int id, RescheduleReservationRequest r, CancellationToken ct)
    {
        if (!actor.IsAdmin) return Result<ReservationDto>.NotFound("Rezervacija nije pronađena.");

        var res = await db.Reservations.Include(x => x.Items).Include(x => x.Slots).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (res is null) return Result<ReservationDto>.NotFound("Rezervacija nije pronađena.");
        if (res.Status is not (ReservationStatus.Confirmed or ReservationStatus.Pending))
            return Result<ReservationDto>.Conflict("Zatvorena rezervacija se ne može pomjeriti.");

        var now = Now;
        var settings = await db.SalonSettings.AsNoTracking().SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var employeeId = r.EmployeeId ?? res.EmployeeId;
        var localDate = DateOnly.FromDateTime(LocalTime.ToLocal(r.StartUtc, zone));

        var loaded = await loader.LoadAsync(employeeId, localDate, res.Items.Select(i => i.ServiceId).ToList(), res.Id, ct);
        if (!loaded.Succeeded) return new Result<ReservationDto>(loaded.Status, Error: loaded.Error);

        // Keep the booked duration (snapshot) even if the catalogue changed since, and allow short notice for staff.
        var query = loaded.Value!.Query with { DurationMinutes = res.TotalDurationMinutes, MinLeadMinutes = 0 };
        if (r.StartUtc < now) return Result<ReservationDto>.Invalid("Termin ne može biti u prošlosti.");
        if (!SlotCalculator.AllStarts(query).Contains(r.StartUtc))
            return Result<ReservationDto>.Conflict("Odabrani termin nije dostupan.");

        var oldStart = res.StartUtc;
        var newSlots = SlotGrid.Occupied(r.StartUtc, res.TotalDurationMinutes, settings.SlotMinutes);

        var conflict = false;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            conflict = false;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                // Two saves on purpose: the old steps must be gone before the new ones are inserted,
                // otherwise they could collide on the unique (employee, step) index.
                db.ReservationSlots.RemoveRange(res.Slots);
                await db.SaveChangesAsync(ct);

                res.EmployeeId = employeeId;
                res.StartUtc = r.StartUtc;
                res.EndUtc = r.StartUtc.AddMinutes(res.TotalDurationMinutes);
                res.Slots = newSlots.Select(t => new ReservationSlot { EmployeeId = employeeId, SlotStartUtc = t }).ToList();
                outbox.Enqueue(new ReservationRescheduled(res.Id, res.ClientId, employeeId, oldStart, r.StartUtc));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException e) when (IsUniqueViolation(e))
            {
                conflict = true;
                await tx.RollbackAsync(ct);
            }
        });

        if (conflict) return Result<ReservationDto>.Conflict(TakenMessage);
        return await GetAsync(actor, id, ct);
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Salon-local dates (inclusive) win over explicit UTC bounds; the server owns the time zone.</summary>
    private async Task<(DateTime? From, DateTime? To)> ResolveRangeAsync(
        DateOnly? fromDate, DateOnly? toDate, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
    {
        if (fromDate is null && toDate is null) return (fromUtc, toUtc);
        var zoneId = await db.SalonSettings.AsNoTracking().Select(s => s.TimeZoneId).SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        return (
            fromDate is { } fd ? LocalTime.DayRangeUtc(fd, zone).Start : fromUtc,
            toDate is { } td ? LocalTime.DayRangeUtc(td, zone).End : toUtc);
    }

    private static bool CanSee(Actor actor, Reservation r) => actor.Kind switch
    {
        ActorKind.Admin => true,
        ActorKind.Employee => r.EmployeeId == actor.UserId,
        _ => r.ClientId == actor.UserId,
    };

    private static bool IsUniqueViolation(DbUpdateException e) =>
        e.InnerException is SqlException { Number: 2601 or 2627 };

    private async Task<bool> IsClientAsync(string userId, CancellationToken ct) =>
        await (from ur in db.UserRoles
               join role in db.Roles on ur.RoleId equals role.Id
               where ur.UserId == userId && role.Name == Roles.Client
               select ur.UserId).AnyAsync(ct);

    private async Task<List<ReservationDto>> ToDtosAsync(Actor actor, IReadOnlyList<Reservation> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var userIds = rows.SelectMany(r => new[] { r.ClientId, r.EmployeeId }).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName }).ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        var settings = await db.SalonSettings.AsNoTracking().SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var now = Now;

        return rows.Select(r => new ReservationDto(
            r.Id, r.ClientId, names.GetValueOrDefault(r.ClientId, ""), r.EmployeeId, names.GetValueOrDefault(r.EmployeeId, ""),
            DateTime.SpecifyKind(r.StartUtc, DateTimeKind.Utc), DateTime.SpecifyKind(r.EndUtc, DateTimeKind.Utc),
            LocalTime.ToLocal(r.StartUtc, zone).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            r.Status.ToString(), r.Note, r.TotalPrice, r.TotalDurationMinutes,
            r.Items.Select(i => new ReservationItemDto(i.ServiceId, i.ServiceName, i.Price, i.DurationMinutes)).ToList(),
            CanCancel: ReservationStateMachine.Can(r.Status, ReservationStatus.Cancelled, actor.Kind, now, r.StartUtc, settings.CancelWindowMinutes).Allowed,
            r.CreatedAtUtc, r.CancelledAtUtc, r.CancelReason)).ToList();
    }
}
