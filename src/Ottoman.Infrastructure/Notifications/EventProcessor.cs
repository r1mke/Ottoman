using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ottoman.Application.Notifications;
using Ottoman.Application.Push;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Infrastructure.Notifications;

/// <summary>
/// Handles one outbox event: composes the messages, e-mails the recipients and writes their in-app inbox rows.
/// RabbitMQ delivers at least once, so an event is recorded in <c>ProcessedEvents</c> in the same transaction
/// as its inbox rows; a redelivery then changes nothing. E-mail goes first so a send failure retries the
/// whole event instead of losing the mail; every mail that went out is recorded (<c>SentEmails</c>), so the retry
/// does not mail the same people again.
/// </summary>
public class EventProcessor(
    OttomanDbContext db,
    IEmailSender email,
    IPushSender push,
    IResetTokenVault vault,
    TimeProvider clock,
    ILogger<EventProcessor> log)
{
    private static readonly CultureInfo Bs = CultureInfo.GetCultureInfo("bs-Latn-BA");

    public async Task ProcessAsync(long outboxId, string type, string payloadJson, CancellationToken ct)
    {
        if (await db.ProcessedEvents.AnyAsync(p => p.OutboxMessageId == outboxId, ct))
        {
            log.LogDebug("Event {Id} already processed; skipping", outboxId);
            return;
        }

        var messages = await ComposeAsync(type, payloadJson, ct);
        if (messages is null)
        {
            log.LogDebug("No handler for event type {Type}", type);
            await MarkProcessedAsync(outboxId, [], ct);
            return;
        }

        var resolved = await ResolveRecipientsAsync(messages, ct);

        await SendMailsAsync(outboxId, resolved, ct);

        await MarkProcessedAsync(outboxId, resolved.Where(x => x.Message.InApp && x.UserId != null).ToList(), ct);

        // The mail and the inbox rows are committed; a push is a courtesy on top and never fails the event.
        await PushAsync(resolved, ct);
    }

    // ---------------------------------------------------------------- composing

    private async Task<IReadOnlyList<Message>?> ComposeAsync(string type, string json, CancellationToken ct)
    {
        switch (type)
        {
            case nameof(UserRegistered):
                return MessageComposer.Welcome(Read<UserRegistered>(json));
            case nameof(RegistrationAttemptedForExistingAccount):
                return MessageComposer.ExistingAccountAttempt(Read<RegistrationAttemptedForExistingAccount>(json));
            case nameof(PasswordResetIgnored):
                return []; // timing cover for an unknown address: nothing to send
            case nameof(PasswordResetRequested):
                {
                    var e = Read<PasswordResetRequested>(json);
                    var token = vault.Open(e.SealedToken);
                    if (token is null)
                    {
                        log.LogWarning("The sealed reset code of event {Type} has expired or is damaged; no mail is sent", type);
                        return [];
                    }
                    return MessageComposer.PasswordReset(e, token);
                }

            case nameof(ReservationCreated):
                {
                    var e = Read<ReservationCreated>(json);
                    var f = await ReservationFactsAsync(e.ReservationId, e.ClientId, e.EmployeeId, e.StartUtc, e.TotalPrice, ct);
                    return MessageComposer.ReservationCreated(f, e.Confirmed);
                }
            case nameof(ReservationConfirmed):
                {
                    var e = Read<ReservationConfirmed>(json);
                    var f = await ReservationFactsAsync(e.ReservationId, e.ClientId, e.EmployeeId, e.StartUtc, null, ct);
                    return MessageComposer.ReservationConfirmed(f);
                }
            case nameof(ReservationCancelled):
                {
                    var e = Read<ReservationCancelled>(json);
                    var f = await ReservationFactsAsync(e.ReservationId, e.ClientId, e.EmployeeId, e.StartUtc, null, ct);
                    return e.Declined
                        ? MessageComposer.ReservationDeclined(f, e.Reason)
                        : MessageComposer.ReservationCancelled(f, e.CancelledBy, e.Reason);
                }
            case nameof(ReservationRescheduled):
                {
                    var e = Read<ReservationRescheduled>(json);
                    var f = await ReservationFactsAsync(e.ReservationId, e.ClientId, e.EmployeeId, e.NewStartUtc, null, ct);
                    return MessageComposer.ReservationRescheduled(f, await LocalAsync(e.OldStartUtc, ct));
                }
            case nameof(ReservationReminderDue):
                {
                    var e = Read<ReservationReminderDue>(json);
                    var f = await ReservationFactsAsync(e.ReservationId, e.ClientId, e.EmployeeId, e.StartUtc, null, ct);
                    return MessageComposer.Reminder(f, Enum.Parse<ReminderKind>(e.Kind));
                }

            case nameof(OrderPlaced):
                {
                    var e = Read<OrderPlaced>(json);
                    return MessageComposer.OrderPlaced(await OrderFactsAsync(e.OrderId, e.ClientId, e.TotalPrice, ct), e.Method);
                }
            case nameof(OrderPaid):
                {
                    var e = Read<OrderPaid>(json);
                    return MessageComposer.OrderPaid(await OrderFactsAsync(e.OrderId, e.ClientId, e.TotalPrice, ct));
                }
            case nameof(OrderCancelled):
                {
                    var e = Read<OrderCancelled>(json);
                    var total = await db.Orders.AsNoTracking().Where(o => o.Id == e.OrderId).Select(o => o.TotalPrice).FirstOrDefaultAsync(ct);
                    return MessageComposer.OrderCancelled(await OrderFactsAsync(e.OrderId, e.ClientId, total, ct), e.CancelledBy);
                }
            case nameof(ProductLowStock):
                return MessageComposer.LowStock(Read<ProductLowStock>(json));

            case nameof(AnnouncementPublished):
                return await AnnouncementMessagesAsync(Read<AnnouncementPublished>(json).AnnouncementId, ct);

            default:
                return null;
        }
    }

    /// <summary>Announcement e-mails: the inbox rows were created with the announcement; here the same people get a mail.</summary>
    private async Task<IReadOnlyList<Message>> AnnouncementMessagesAsync(int announcementId, CancellationToken ct)
    {
        var a = await db.Announcements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == announcementId, ct);
        if (a is null) return [];
        var userIds = await db.UserNotifications.AsNoTracking()
            .Where(n => n.AnnouncementId == announcementId).Select(n => n.UserId).ToListAsync(ct);
        // InApp is false: the row already exists, writing it again would show the message twice.
        return userIds.Select(id => new Message(a.Title, a.Body, "Announcement", UserId: id, InApp: false, SendEmail: a.SendEmail)).ToList();
    }

    private static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} payload.");

    // ---------------------------------------------------------------- facts

    private async Task<string> LocalAsync(DateTime utc, CancellationToken ct)
    {
        var zoneId = await db.SalonSettings.AsNoTracking().Select(s => s.TimeZoneId).FirstAsync(ct);
        var local = LocalTime.ToLocal(utc, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
        return local.ToString("dd.MM.yyyy. 'u' HH:mm", Bs);
    }

    private async Task<Party> PartyAsync(string userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Where(x => x.Id == userId)
            .Select(x => new { x.Email, x.FirstName, x.LastName }).FirstOrDefaultAsync(ct);
        return u is null
            ? new Party(userId, "korisnik", null)
            : new Party(userId, $"{u.FirstName} {u.LastName}".Trim(), u.Email);
    }

    private async Task<ReservationFacts> ReservationFactsAsync(
        int reservationId, string clientId, string employeeId, DateTime startUtc, decimal? total, CancellationToken ct)
    {
        var names = await db.ReservationItems.AsNoTracking().Where(i => i.ReservationId == reservationId)
            .Select(i => i.ServiceName).ToListAsync(ct);
        var price = total ?? await db.Reservations.AsNoTracking().Where(r => r.Id == reservationId)
            .Select(r => r.TotalPrice).FirstOrDefaultAsync(ct);
        return new ReservationFacts(
            reservationId, await PartyAsync(clientId, ct), await PartyAsync(employeeId, ct),
            await LocalAsync(startUtc, ct), string.Join(", ", names), price);
    }

    private async Task<OrderFacts> OrderFactsAsync(int orderId, string clientId, decimal total, CancellationToken ct)
    {
        var items = await db.OrderItems.AsNoTracking().Where(i => i.OrderId == orderId)
            .Select(i => new { i.Quantity, i.ProductName }).ToListAsync(ct);
        return new OrderFacts(orderId, await PartyAsync(clientId, ct), total,
            string.Join(", ", items.Select(i => $"{i.Quantity}× {i.ProductName}")));
    }

    // ---------------------------------------------------------------- recipients

    private sealed record Resolved(Message Message, string? UserId, string? Email, string? Name);

    private async Task<List<Resolved>> ResolveRecipientsAsync(IReadOnlyList<Message> messages, CancellationToken ct)
    {
        var result = new List<Resolved>();
        List<(string Id, string? Email, string Name)>? admins = null;

        foreach (var m in messages)
        {
            if (m.ToAdmins)
            {
                admins ??= await AdminsAsync(ct);
                result.AddRange(admins.Select(a => new Resolved(m, a.Id, a.Email, a.Name)));
            }
            else if (m.UserId is not null)
            {
                var p = await PartyAsync(m.UserId, ct);
                result.Add(new Resolved(m, m.UserId, p.Email, p.Name));
            }
            else if (m.Email is not null)
            {
                result.Add(new Resolved(m, null, m.Email, null));
            }
        }
        return result;
    }

    private async Task<List<(string Id, string? Email, string Name)>> AdminsAsync(CancellationToken ct)
    {
        var rows = await (
            from ur in db.UserRoles
            join role in db.Roles on ur.RoleId equals role.Id
            join u in db.Users on ur.UserId equals u.Id
            where role.Name == Roles.Admin && !u.IsBlocked
            select new { u.Id, u.Email, u.FirstName, u.LastName }).ToListAsync(ct);
        return rows.Select(r => (r.Id, (string?)r.Email, $"{r.FirstName} {r.LastName}".Trim())).ToList();
    }

    // ---------------------------------------------------------------- push

    private async Task PushAsync(IReadOnlyList<Resolved> recipients, CancellationToken ct)
    {
        try
        {
            var targets = recipients.Where(r => r.UserId != null && PushPolicy.ShouldPush(r.Message.Kind)).ToList();
            if (targets.Count == 0) return;

            var ids = targets.Select(t => t.UserId!).Distinct().ToList();
            var devices = await db.DeviceTokens.AsNoTracking().Where(d => ids.Contains(d.UserId))
                .Select(d => new { d.UserId, d.Token }).ToListAsync(ct);
            if (devices.Count == 0) return;

            var dead = new HashSet<string>();
            foreach (var t in targets)
            {
                var message = new PushMessage(t.Message.Title, t.Message.Body, t.Message.Kind);
                foreach (var d in devices.Where(d => d.UserId == t.UserId))
                    if (await push.SendAsync(d.Token, message, ct) == PushOutcome.InvalidToken) dead.Add(d.Token);
            }

            if (dead.Count > 0)
            {
                await db.DeviceTokens.Where(d => dead.Contains(d.Token)).ExecuteDeleteAsync(ct);
                log.LogInformation("Forgot {Count} dead device token(s)", dead.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Push step failed; the event itself was handled");
        }
    }

    // ---------------------------------------------------------------- e-mail

    /// <summary>
    /// Sends the event's mails, each recorded right after it went out. A failure part-way retries the whole event,
    /// and the retry skips everyone who already got their mail (at most the one mail whose record failed repeats).
    /// </summary>
    private async Task SendMailsAsync(long outboxId, IReadOnlyList<Resolved> resolved, CancellationToken ct)
    {
        var mails = resolved.Where(x => x.Message.SendEmail && !string.IsNullOrWhiteSpace(x.Email)).ToList();
        if (mails.Count == 0) return;

        var done = (await db.SentEmails.AsNoTracking().Where(s => s.OutboxMessageId == outboxId)
            .Select(s => s.RecipientKey).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        foreach (var r in mails)
        {
            var key = MailKey(r.Email!, r.Message);
            if (!done.Add(key)) continue; // sent by an earlier attempt (or the same mail twice in one event)
            await email.SendAsync(
                r.Email!, r.Name ?? r.Email!, r.Message.Title,
                EmailTemplate.Html(r.Message.Title, r.Message.Body), r.Message.Body, ct);
            await RecordSentAsync(outboxId, key, ct);
        }
    }

    /// <summary>Identifies one mail of an event without storing its text (a reset mail carries a code).</summary>
    private static string MailKey(string address, Message message) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{address.Trim().ToLowerInvariant()}\n{message.Kind}\n{message.Title}")));

    private async Task RecordSentAsync(long outboxId, string key, CancellationToken ct)
    {
        db.SentEmails.Add(new SentEmail { OutboxMessageId = outboxId, RecipientKey = key, SentAtUtc = clock.GetUtcNow().UtcDateTime });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent consumer recorded the same mail.
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    // ---------------------------------------------------------------- persistence

    private async Task MarkProcessedAsync(long outboxId, IReadOnlyList<Resolved> inApp, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                db.ProcessedEvents.Add(new ProcessedEvent { OutboxMessageId = outboxId, ProcessedAtUtc = now });
                db.UserNotifications.AddRange(inApp.Select(r => new UserNotification
                {
                    UserId = r.UserId!,
                    Title = r.Message.Title,
                    Body = r.Message.Body,
                    Kind = r.Message.Kind,
                    CreatedAtUtc = now,
                }));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // A concurrent consumer handled the same event first; nothing more to do.
            log.LogDebug("Event {Id} was processed concurrently", outboxId);
        }
    }
}
