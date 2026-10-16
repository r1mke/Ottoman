using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Notifications;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Notifications;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Notifications;

public class NotificationService(OttomanDbContext db, IOutbox outbox, TimeProvider clock) : INotificationService
{
    // ---------------------------------------------------------------- inbox (always the caller's own rows)

    public async Task<PagedResult<NotificationDto>> ListAsync(string userId, bool unreadOnly, PageQuery page, CancellationToken ct)
    {
        var query = db.UserNotifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => n.ReadAtUtc == null);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.Kind, n.CreatedAtUtc, n.ReadAtUtc != null))
            .ToListAsync(ct);
        return new PagedResult<NotificationDto>(items, total);
    }

    public Task<int> UnreadCountAsync(string userId, CancellationToken ct) =>
        db.UserNotifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, ct);

    public async Task<Result<bool>> MarkReadAsync(string userId, long id, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        // Someone else's notification is indistinguishable from one that does not exist.
        var exists = await db.UserNotifications.AnyAsync(n => n.Id == id && n.UserId == userId, ct);
        if (!exists) return Result<bool>.NotFound("Obavijest nije pronađena.");
        await db.UserNotifications.Where(n => n.Id == id && n.UserId == userId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, now), ct);
        return Result<bool>.Ok(true);
    }

    public Task<int> MarkAllReadAsync(string userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.UserNotifications.Where(n => n.UserId == userId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, now), ct);
    }

    // ---------------------------------------------------------------- announcements (admin)

    public async Task<Result<AnnouncementDto>> CreateAnnouncementAsync(string adminId, AnnouncementRequest r, CancellationToken ct)
    {
        List<string> recipients;
        if (r.Audience == AnnouncementAudience.User)
        {
            var ok = await db.Users.AnyAsync(u => u.Id == r.TargetUserId && !u.IsBlocked, ct);
            if (!ok) return Result<AnnouncementDto>.Invalid("Odabrani korisnik ne postoji.");
            recipients = [r.TargetUserId!];
        }
        else
        {
            var roles = AudienceRules.RolesFor(r.Audience);
            recipients = await (
                from ur in db.UserRoles
                join role in db.Roles on ur.RoleId equals role.Id
                join u in db.Users on ur.UserId equals u.Id
                where roles.Contains(role.Name!) && !u.IsBlocked
                select u.Id).Distinct().ToListAsync(ct);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var announcement = new Announcement
        {
            Title = r.Title.Trim(),
            Body = r.Body.Trim(),
            Audience = r.Audience,
            TargetUserId = r.Audience == AnnouncementAudience.User ? r.TargetUserId : null,
            SendEmail = r.SendEmail,
            RecipientCount = recipients.Count,
            CreatedByUserId = adminId,
            CreatedAtUtc = now,
        };

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            db.Announcements.Add(announcement);
            await db.SaveChangesAsync(ct); // assigns the id the rows and the event refer to

            db.UserNotifications.AddRange(recipients.Select(id => new UserNotification
            {
                UserId = id,
                Title = announcement.Title,
                Body = announcement.Body,
                Kind = "Announcement",
                CreatedAtUtc = now,
                AnnouncementId = announcement.Id,
            }));
            if (r.SendEmail) outbox.Enqueue(new AnnouncementPublished(announcement.Id));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return Result<AnnouncementDto>.Ok(ToDto(announcement));
    }

    public async Task<PagedResult<AnnouncementDto>> ListAnnouncementsAsync(PageQuery page, CancellationToken ct)
    {
        var total = await db.Announcements.CountAsync(ct);
        var items = await db.Announcements.AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .ToListAsync(ct);
        return new PagedResult<AnnouncementDto>(items.Select(ToDto).ToList(), total);
    }

    private static AnnouncementDto ToDto(Announcement a) =>
        new(a.Id, a.Title, a.Body, a.Audience, a.SendEmail, a.RecipientCount, a.CreatedAtUtc);
}
