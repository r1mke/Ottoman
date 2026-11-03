using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Notifications;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Notifications;

/// <summary>
/// Finds appointments that are due a reminder and queues one event per reminder. The reminder row and its event
/// are saved together, and UNIQUE(ReservationId, Kind) means two schedulers (or a restart) can never send twice.
/// </summary>
public class ReminderService(OttomanDbContext db, IOutbox outbox, TimeProvider clock)
{
    public async Task<int> EnqueueDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var horizon = now + ReminderPolicy.DayBefore;

        var upcoming = await db.Reservations.AsNoTracking()
            .Where(r => (r.Status == ReservationStatus.Confirmed || r.Status == ReservationStatus.Pending)
                        && r.StartUtc > now && r.StartUtc <= horizon)
            .Select(r => new { r.Id, r.ClientId, r.EmployeeId, r.StartUtc, r.CreatedAtUtc })
            .ToListAsync(ct);
        if (upcoming.Count == 0) return 0;

        var ids = upcoming.Select(r => r.Id).ToList();
        var sent = (await db.ReservationReminders.AsNoTracking().Where(x => ids.Contains(x.ReservationId))
                .Select(x => new { x.ReservationId, x.Kind }).ToListAsync(ct))
            .GroupBy(x => x.ReservationId).ToDictionary(g => g.Key, g => g.Select(x => x.Kind).ToHashSet());

        var due = upcoming
            .SelectMany(r => ReminderPolicy.Due(now, r.StartUtc, r.CreatedAtUtc, sent.GetValueOrDefault(r.Id) ?? [])
                .Select(kind => new ReservationReminderDue(r.Id, r.ClientId, r.EmployeeId, r.StartUtc, kind.ToString())))
            .ToList();
        if (due.Count == 0) return 0;

        // Normally all reminders and their events go in one save (one transaction).
        db.ChangeTracker.Clear();
        foreach (var e in due) Queue(e, now);
        try
        {
            await db.SaveChangesAsync(ct);
            return due.Count;
        }
        catch (DbUpdateException ex) when (IsDuplicate(ex))
        {
            // Another scheduler queued some of them first; nothing of this batch was saved. Go one by one so the
            // rest still go out exactly once.
        }

        var queued = 0;
        foreach (var e in due)
        {
            db.ChangeTracker.Clear();
            Queue(e, now);
            try
            {
                await db.SaveChangesAsync(ct);
                queued++;
            }
            catch (DbUpdateException ex) when (IsDuplicate(ex))
            {
                // Another instance queued this reminder first.
            }
        }
        db.ChangeTracker.Clear();
        return queued;
    }

    private void Queue(ReservationReminderDue e, DateTime now)
    {
        db.ReservationReminders.Add(new ReservationReminder { ReservationId = e.ReservationId, Kind = Enum.Parse<ReminderKind>(e.Kind), SentAtUtc = now });
        outbox.Enqueue(e);
    }

    private static bool IsDuplicate(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };
}
