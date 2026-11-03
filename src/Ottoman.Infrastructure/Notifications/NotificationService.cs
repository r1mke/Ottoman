using Microsoft.EntityFrameworkCore;
using Ottoman.Application.Common;
using Ottoman.Application.Images;
using Ottoman.Application.Notifications;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Notifications;
using Ottoman.Infrastructure.Images;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Notifications;

public class NotificationService(
    OttomanDbContext db, IOutbox outbox, TimeProvider clock, IImageAttachments attachments, ImageUrlBuilder images) : INotificationService
{
    // ---------------------------------------------------------------- inbox (always the caller's own rows)

    public async Task<PagedResult<NotificationDto>> ListAsync(string userId, bool unreadOnly, PageQuery page, CancellationToken ct)
    {
        var query = db.UserNotifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => n.ReadAtUtc == null);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(n => new
            {
                n.Id, n.Title, n.Body, n.Kind, n.CreatedAtUtc,
                IsRead = n.ReadAtUtc != null,
                // A news item carries its picture into every recipient's inbox.
                ImageKey = db.Announcements.Where(a => a.Id == n.AnnouncementId).Select(a => a.ImageKey).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return new PagedResult<NotificationDto>(
            items.Select(n => new NotificationDto(n.Id, n.Title, n.Body, n.Kind, n.CreatedAtUtc, n.IsRead, images.Url(n.ImageKey))).ToList(),
            total);
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

    public async Task<Result<AnnouncementDto>> CreateAnnouncementAsync(
        string adminId, AnnouncementRequest r, Stream? image, CancellationToken ct)
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

        string? imageKey = null;
        if (image is not null)
        {
            var uploaded = await attachments.UploadAnnouncementImageAsync(image, ct);
            if (!uploaded.Succeeded) return Result<AnnouncementDto>.Invalid(uploaded.Error!);
            imageKey = uploaded.Value;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var announcement = new Announcement
        {
            ImageKey = imageKey,
            Title = r.Title.Trim(),
            Body = r.Body.Trim(),
            Audience = r.Audience,
            TargetUserId = r.Audience == AnnouncementAudience.User ? r.TargetUserId : null,
            SendEmail = r.SendEmail,
            RecipientCount = recipients.Count,
            CreatedByUserId = adminId,
            CreatedAtUtc = now,
        };

        try
        {
            await SaveAnnouncementAsync(announcement, recipients, now, ct);
        }
        catch
        {
            if (imageKey is not null) await attachments.DiscardImageAsync(imageKey);
            throw;
        }

        return Result<AnnouncementDto>.Ok(ToDto(announcement));
    }

    private async Task SaveAnnouncementAsync(Announcement announcement, List<string> recipients, DateTime now, CancellationToken ct)
    {
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
            // Always published: the worker mails it when asked to and pushes it to the recipients phones.
            outbox.Enqueue(new AnnouncementPublished(announcement.Id));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
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

    public async Task<Result<AnnouncementDto>> GetAnnouncementAsync(int id, CancellationToken ct)
    {
        var a = await db.Announcements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? Result<AnnouncementDto>.NotFound("Obavijest nije pronađena.") : Result<AnnouncementDto>.Ok(ToDto(a));
    }

    private AnnouncementDto ToDto(Announcement a) =>
        new(a.Id, a.Title, a.Body, a.Audience, a.SendEmail, a.RecipientCount, a.CreatedAtUtc, images.Url(a.ImageKey));
}
