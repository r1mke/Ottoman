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

    /// <summary>
    /// The caller's rows, minus those of announcements the admin hid or that expired: the state of the announcement is
    /// checked at read time, so hiding or an end date takes effect at once for every recipient.
    /// </summary>
    private IQueryable<UserNotification> Inbox(string userId)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.UserNotifications.Where(n => n.UserId == userId
            && (n.AnnouncementId == null || db.Announcements.Any(a => a.Id == n.AnnouncementId
                && !a.IsHidden && (a.ExpiresAtUtc == null || a.ExpiresAtUtc > now))));
    }

    public async Task<PagedResult<NotificationDto>> ListAsync(string userId, bool unreadOnly, PageQuery page, CancellationToken ct)
    {
        var query = Inbox(userId).AsNoTracking();
        if (unreadOnly) query = query.Where(n => n.ReadAtUtc == null);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip(page.Skip).Take(page.SafePageSize)
            .Select(n => new
            {
                n.Id, n.Title, n.Body, n.Kind, n.CreatedAtUtc,
                IsRead = n.ReadAtUtc != null,
                // A news item shows its current text, picture and category (the admin may have edited it since).
                Announcement = db.Announcements.Where(a => a.Id == n.AnnouncementId)
                    .Select(a => new { a.Title, a.Body, a.ImageKey, a.Category }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return new PagedResult<NotificationDto>(
            items.Select(n => new NotificationDto(
                n.Id, n.Announcement?.Title ?? n.Title, n.Announcement?.Body ?? n.Body, n.Kind, n.CreatedAtUtc, n.IsRead,
                images.Url(n.Announcement?.ImageKey), n.Announcement?.Category)).ToList(),
            total);
    }

    public Task<int> UnreadCountAsync(string userId, CancellationToken ct) =>
        Inbox(userId).CountAsync(n => n.ReadAtUtc == null, ct);

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

        var now = clock.GetUtcNow().UtcDateTime;
        if (AnnouncementVisibility.Expired(r.ExpiresAtUtc, now)) return Result<AnnouncementDto>.Invalid(AnnouncementVisibility.ExpiryInPast);

        string? imageKey = null;
        if (image is not null)
        {
            var uploaded = await attachments.UploadAnnouncementImageAsync(image, ct);
            if (!uploaded.Succeeded) return Result<AnnouncementDto>.Invalid(uploaded.Error!);
            imageKey = uploaded.Value;
        }

        var announcement = new Announcement
        {
            ImageKey = imageKey,
            Category = r.Category,
            ExpiresAtUtc = r.ExpiresAtUtc is { } exp ? DateTime.SpecifyKind(exp, DateTimeKind.Utc) : null,
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

    public async Task<PagedResult<AnnouncementDto>> ListAnnouncementsAsync(string? search, PageQuery page, CancellationToken ct)
    {
        var query = db.Announcements.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(a => a.Title.Contains(term) || a.Body.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query
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

    public async Task<Result<AnnouncementDto>> UpdateAnnouncementAsync(int id, UpdateAnnouncementRequest r, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (AnnouncementVisibility.Expired(r.ExpiresAtUtc, now)) return Result<AnnouncementDto>.Invalid(AnnouncementVisibility.ExpiryInPast);
        var expires = r.ExpiresAtUtc is { } exp ? DateTime.SpecifyKind(exp, DateTimeKind.Utc) : (DateTime?)null;
        // One statement; no event is published, so nobody gets the message again.
        var rows = await db.Announcements.Where(a => a.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.Title, r.Title.Trim())
            .SetProperty(a => a.Body, r.Body.Trim())
            .SetProperty(a => a.Category, r.Category)
            .SetProperty(a => a.ExpiresAtUtc, expires)
            .SetProperty(a => a.UpdatedAtUtc, now), ct);
        return rows == 0 ? Result<AnnouncementDto>.NotFound("Obavijest nije pronađena.") : await GetAnnouncementAsync(id, ct);
    }

    public async Task<Result<AnnouncementDto>> SetAnnouncementHiddenAsync(int id, bool hidden, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await db.Announcements.Where(a => a.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.IsHidden, hidden)
            .SetProperty(a => a.UpdatedAtUtc, now), ct);
        return rows == 0 ? Result<AnnouncementDto>.NotFound("Obavijest nije pronađena.") : await GetAnnouncementAsync(id, ct);
    }

    public async Task<Result<AnnouncementDto>> SetAnnouncementImageAsync(int id, Stream? image, CancellationToken ct)
    {
        var replaced = await attachments.ReplaceAnnouncementImageAsync(id, image, ct);
        if (!replaced.Succeeded)
            return replaced.Status == ResultStatus.NotFound
                ? Result<AnnouncementDto>.NotFound("Obavijest nije pronađena.")
                : new Result<AnnouncementDto>(replaced.Status, Error: replaced.Error);
        return await GetAnnouncementAsync(id, ct);
    }

    private AnnouncementDto ToDto(Announcement a) =>
        new(a.Id, a.Title, a.Body, a.Audience, a.SendEmail, a.RecipientCount, a.CreatedAtUtc, images.Url(a.ImageKey),
            a.Category, a.ExpiresAtUtc, a.IsHidden, AnnouncementVisibility.Expired(a.ExpiresAtUtc, clock.GetUtcNow().UtcDateTime),
            a.UpdatedAtUtc);
}
