using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Catalog;
using Ottoman.Application.Common;
using Ottoman.Application.Notifications;
using Ottoman.Application.Reservations;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Notifications;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

public sealed record SentMail(string To, string Name, string Subject, string Html, string Text);

/// <summary>Records mail instead of sending it; can be told to fail to exercise the retry path.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<SentMail> _sent = new();
    public volatile bool Fail;

    /// <summary>When set to n >= 0: n more mails go out, then one send fails (and the counter switches off).</summary>
    public int FailAfter = -1;

    public IReadOnlyList<SentMail> Sent => [.. _sent];

    public Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string textBody, CancellationToken ct)
    {
        if (Fail) throw new InvalidOperationException("SMTP is down (test)");
        if (FailAfter == 0)
        {
            FailAfter = -1;
            throw new InvalidOperationException("SMTP dropped the connection (test)");
        }
        if (FailAfter > 0) FailAfter--;
        _sent.Enqueue(new SentMail(toEmail, toName, subject, htmlBody, textBody));
        return Task.CompletedTask;
    }
}

[Collection("api")]
public class NotificationTests(ApiFixture api)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private FakeEmailSender Mail => api.Services.GetRequiredService<FakeEmailSender>();

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, string Email, HttpClient Http)> NewClient()
    {
        var (email, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, email, api.CreateClient().As(auth));
    }

    private async Task<(string Id, string Email, HttpClient Http)> Kerim()
    {
        var auth = await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword);
        return (auth.User.Id, "kerim@ottoman.local", api.CreateClient().As(auth));
    }

    private async Task Process(OutboxMessage m)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EventProcessor>().ProcessAsync(m.Id, m.Type, m.PayloadJson, default);
    }

    private async Task<OutboxMessage> OutboxOf(string type, Func<string, bool> payload)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var all = await db.OutboxMessages.AsNoTracking().Where(m => m.Type == type).OrderByDescending(m => m.Id).Take(200).ToListAsync();
        return all.First(m => payload(m.PayloadJson));
    }

    private async Task<List<NotificationDto>> Inbox(HttpClient http, bool unreadOnly = false) =>
        (await http.GetFromJsonAsync<PagedResult<NotificationDto>>($"/api/v1/notifications?unreadOnly={unreadOnly}&pageSize=100"))!.Items.ToList();

    // ------------------------------------------------------------------ inbox

    [Fact]
    public async Task The_inbox_needs_login_and_starts_empty()
    {
        (await api.CreateClient().GetAsync("/api/v1/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (_, _, client) = await NewClient();
        (await Inbox(client)).Should().BeEmpty();
        (await client.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count"))!.Count.Should().Be(0);
    }

    [Fact]
    public async Task An_announcement_reaches_exactly_the_chosen_group()
    {
        var (_, _, client) = await NewClient();
        var (_, _, barber) = await Kerim();
        var admin = await Admin();
        var title = $"Akcija {Guid.NewGuid():N}";

        var res = await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest(title, "Šišanje -10%", AnnouncementAudience.Clients));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await res.Content.ReadFromJsonAsync<AnnouncementDto>())!;
        dto.RecipientCount.Should().BeGreaterThanOrEqualTo(1);
        (await Inbox(client)).Should().ContainSingle(n => n.Title == title && n.Kind == "Announcement" && !n.IsRead);
        (await Inbox(barber)).Should().NotContain(n => n.Title == title);
        (await Inbox(admin)).Should().NotContain(n => n.Title == title);
    }

    [Fact]
    public async Task A_personal_message_reaches_only_that_user()
    {
        var (aId, _, a) = await NewClient();
        var (_, _, b) = await NewClient();
        var admin = await Admin();
        var title = $"Samo za tebe {Guid.NewGuid():N}";

        (await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest(title, "Zdravo", AnnouncementAudience.User, aId)))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await Inbox(a)).Should().Contain(n => n.Title == title);
        (await Inbox(b)).Should().NotContain(n => n.Title == title);
    }

    [Fact]
    public async Task Announcements_are_admin_only_and_validated()
    {
        var (_, _, client) = await NewClient();
        var (_, _, barber) = await Kerim();
        var body = new AnnouncementRequest("T", "B", AnnouncementAudience.All);
        (await client.PostAsJsonAsync("/api/v1/announcements", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await barber.PostAsJsonAsync("/api/v1/announcements", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/announcements")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await Admin();
        (await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest("", "B", AnnouncementAudience.All))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest("T", "B", AnnouncementAudience.User))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest("T", "B", AnnouncementAudience.User, "no-such-user"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Timestamps_are_sent_as_UTC_with_a_Z_so_phones_do_not_read_them_as_local_time()
    {
        var (id, _, client) = await NewClient();
        var admin = await Admin();
        await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest("UTC provjera", "x", AnnouncementAudience.User, id));

        var raw = await client.GetStringAsync("/api/v1/notifications");
        System.Text.RegularExpressions.Regex.IsMatch(raw, "\"createdAtUtc\":\"[^\"]+Z\"").Should().BeTrue(raw);
    }

    [Fact]
    public async Task The_admin_sees_the_history_of_what_was_sent()
    {
        var admin = await Admin();
        var title = $"Povijest {Guid.NewGuid():N}";
        await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest(title, "x", AnnouncementAudience.Employees));

        var page = (await admin.GetFromJsonAsync<PagedResult<AnnouncementDto>>("/api/v1/announcements?pageSize=50"))!;
        page.Items.Should().Contain(a => a.Title == title && a.Audience == AnnouncementAudience.Employees);
    }

    [Fact]
    public async Task Read_state_is_per_user_and_mark_all_clears_the_badge()
    {
        var (id, _, client) = await NewClient();
        var (_, _, other) = await NewClient();
        var admin = await Admin();
        for (var i = 0; i < 3; i++)
            await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest($"Poruka {i} {Guid.NewGuid():N}", "x", AnnouncementAudience.User, id));

        (await client.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count"))!.Count.Should().Be(3);
        var first = (await Inbox(client)).First();

        (await client.PostAsync($"/api/v1/notifications/{first.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count"))!.Count.Should().Be(2);
        (await Inbox(client, unreadOnly: true)).Should().HaveCount(2);

        // someone else's notification looks like it does not exist
        (await other.PostAsync($"/api/v1/notifications/{first.Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await client.PostAsync("/api/v1/notifications/read-all", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count"))!.Count.Should().Be(0);
        (await Inbox(client)).Should().HaveCount(3).And.OnlyContain(n => n.IsRead);
    }

    // ------------------------------------------------------------------ events -> mail + inbox

    [Fact]
    public async Task Registration_sends_a_welcome_mail_and_escapes_the_name()
    {
        var email = ApiTestEmail();
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Test123!x",
            firstName = "<img src=x onerror=alert(1)>",
            lastName = "Test",
        })).StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.BadRequest);

        // Some validators may reject markup in names outright; either way no raw markup may reach an e-mail.
        var evt = await TryOutbox("UserRegistered", p => p.Contains(email));
        if (evt is null) return;
        await Process(evt);

        var mail = Mail.Sent.Single(m => m.To == email);
        mail.Subject.Should().Contain("Dobrodošli");
        mail.Html.Should().NotContain("<img src=x").And.Contain("&lt;img");
    }

    [Fact]
    public async Task An_announcement_mail_that_fails_half_way_is_not_sent_twice_to_anyone_on_retry()
    {
        var (_, firstEmail, _) = await NewClient();
        var (_, secondEmail, _) = await NewClient();
        var admin = await Admin();
        var title = $"Mail svima {Guid.NewGuid():N}";
        var res = await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest(title, "Zdravo", AnnouncementAudience.Clients, SendEmail: true));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await res.Content.ReadFromJsonAsync<AnnouncementDto>())!.Id;
        var evt = await OutboxOf("AnnouncementPublished", p => p.Contains($"\"AnnouncementId\":{id}}}"));

        Mail.FailAfter = 1; // one mail goes out, the next send fails
        var first = async () => await Process(evt);
        await first.Should().ThrowAsync<InvalidOperationException>();
        Mail.Sent.Count(m => m.Subject == title).Should().Be(1);

        await Process(evt); // the broker redelivers the event
        await Process(evt); // ...and once more after it was handled

        var mails = Mail.Sent.Where(m => m.Subject == title).ToList();
        mails.GroupBy(m => m.To).Should().OnlyContain(g => g.Count() == 1, "nobody gets the same announcement twice");
        mails.Select(m => m.To).Should().Contain([firstEmail, secondEmail]);
    }

    private static string ApiTestEmail() => $"mail{Guid.NewGuid():N}@test.local";

    private async Task<OutboxMessage?> TryOutbox(string type, Func<string, bool> payload)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        return (await db.OutboxMessages.AsNoTracking().Where(m => m.Type == type).OrderByDescending(m => m.Id).Take(200).ToListAsync())
            .FirstOrDefault(m => payload(m.PayloadJson));
    }

    [Fact]
    public async Task A_booking_notifies_the_client_and_the_barber_by_mail_and_in_the_inbox_exactly_once()
    {
        var (clientId, clientEmail, client) = await NewClient();
        var (kerimId, kerimEmail, kerim) = await Kerim();
        var service = (await client.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Single(s => s.Name == "Šišanje").Id;
        var day = Days.Next();
        var start = LocalTime.ToUtc(day, new TimeOnly(11, 0), Zone);

        var res = await client.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest(kerimId, [service], start, null, null));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var reservation = (await res.Content.ReadFromJsonAsync<ReservationDto>())!;

        var evt = await OutboxOf("ReservationCreated", p => p.Contains($"\"ReservationId\":{reservation.Id},"));
        await Process(evt);
        await Process(evt); // redelivery from the broker

        Mail.Sent.Where(m => m.To == clientEmail).Should().ContainSingle(m => m.Subject == "Termin potvrđen" && m.Text.Contains("Kerim Begić"));
        Mail.Sent.Should().Contain(m => m.To == kerimEmail && m.Subject == "Nova rezervacija");
        (await Inbox(client)).Should().ContainSingle(n => n.Kind == "ReservationCreated" && n.Title == "Termin potvrđen");
        (await Inbox(kerim)).Should().Contain(n => n.Kind == "ReservationCreated" && n.Title == "Nova rezervacija");
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.UserNotifications.CountAsync(n => n.UserId == clientId && n.Kind == "ReservationCreated")).Should().Be(1, "the second delivery must change nothing");
    }

    [Fact]
    public async Task A_failed_mail_leaves_no_trace_so_the_retry_does_everything()
    {
        var (clientId, clientEmail, client) = await NewClient();
        var (kerimId, _, _) = await Kerim();
        var service = (await client.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Single(s => s.Name == "Šišanje").Id;
        var start = LocalTime.ToUtc(Days.Next(), new TimeOnly(15, 0), Zone);
        var reservation = (await (await client.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest(kerimId, [service], start, null, null)))
            .Content.ReadFromJsonAsync<ReservationDto>())!;
        var evt = await OutboxOf("ReservationCreated", p => p.Contains($"\"ReservationId\":{reservation.Id},"));

        Mail.Fail = true;
        try
        {
            var act = () => Process(evt);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            Mail.Fail = false;
        }

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            (await db.UserNotifications.CountAsync(n => n.UserId == clientId)).Should().Be(0);
            (await db.ProcessedEvents.AnyAsync(p => p.OutboxMessageId == evt.Id)).Should().BeFalse();
        }

        await Process(evt);
        Mail.Sent.Should().Contain(m => m.To == clientEmail && m.Subject == "Termin potvrđen");
        (await Inbox(client)).Should().ContainSingle(n => n.Kind == "ReservationCreated");
    }

    [Fact]
    public async Task Cancelling_by_the_client_mails_the_barber_and_not_the_client()
    {
        var (_, clientEmail, client) = await NewClient();
        var (kerimId, kerimEmail, _) = await Kerim();
        var service = (await client.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Single(s => s.Name == "Šišanje").Id;
        var start = LocalTime.ToUtc(Days.Next(), new TimeOnly(16, 0), Zone);
        var reservation = (await (await client.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest(kerimId, [service], start, null, null)))
            .Content.ReadFromJsonAsync<ReservationDto>())!;
        (await client.PostAsJsonAsync($"/api/v1/reservations/{reservation.Id}/cancel", new { reason = (string?)null })).StatusCode.Should().Be(HttpStatusCode.OK);

        var evt = await OutboxOf("ReservationCancelled", p => p.Contains($"\"ReservationId\":{reservation.Id},"));
        var before = Mail.Sent.Count;
        await Process(evt);

        var newMail = Mail.Sent.Skip(before).ToList();
        newMail.Should().Contain(m => m.To == kerimEmail && m.Subject == "Termin otkazan");
        newMail.Should().NotContain(m => m.To == clientEmail);
        (await Inbox(client)).Should().Contain(n => n.Kind == "ReservationCancelled"); // in-app confirmation still shows
    }

    [Fact]
    public async Task Forgot_password_mails_the_token_and_it_is_not_stored_in_any_inbox()
    {
        var (clientId, email, client) = await NewClient();
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).EnsureSuccessStatusCode();

        var evt = await OutboxOf("PasswordResetRequested", p => p.Contains(email));
        await Process(evt);

        var mail = Mail.Sent.Single(m => m.To == email && m.Subject == "Resetovanje lozinke");
        var sealedToken = JsonDocument.Parse(evt.PayloadJson).RootElement.GetProperty("SealedToken").GetString()!;
        var token = api.Services.GetRequiredService<Ottoman.Application.Notifications.IResetTokenVault>().Open(sealedToken)!;
        evt.PayloadJson.Should().NotContain(token, "the reset code must not sit in the outbox as plain text");
        mail.Text.Should().Contain(token);
        (await Inbox(client)).Should().BeEmpty();
        _ = clientId;
    }

    [Fact]
    public async Task The_existing_account_notice_goes_by_mail_to_the_address_and_nowhere_else()
    {
        var (_, email, _) = await NewClient();
        // registering again with the same address is silent for the caller...
        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Test123!x", firstName = "A", lastName = "B" }))
            .StatusCode.Should().Be(HttpStatusCode.Accepted);

        // ...but the real owner is warned
        var evt = await OutboxOf("RegistrationAttemptedForExistingAccount", p => p.Contains(email));
        await Process(evt);
        Mail.Sent.Should().Contain(m => m.To == email && m.Subject == "Pokušaj registracije");
    }

    [Fact]
    public async Task Low_stock_alerts_the_admin_inbox()
    {
        var admin = await Admin();
        var name = $"Rijedak {Guid.NewGuid():N}";
        var created = await admin.PostAsJsonAsync("/api/v1/products", new { name, price = 5m, stock = 3, lowStockThreshold = 5 });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var (_, _, client) = await NewClient();
        var productId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await client.PostAsJsonAsync("/api/v1/orders", new { items = new[] { new { productId, quantity = 1 } }, fulfilment = "Pickup", paymentMethod = "OnReceipt" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var evt = await OutboxOf("ProductLowStock", p => p.Contains(name));
        await Process(evt);

        (await Inbox(admin)).Should().Contain(n => n.Kind == "ProductLowStock" && n.Body.Contains(name));
    }

    // ------------------------------------------------------------------ reminders

    [Fact]
    public async Task Reminders_are_queued_once_per_kind_and_not_for_cancelled_or_late_bookings()
    {
        var (clientId, _, _) = await NewClient();
        var (kerimId, _, _) = await Kerim();
        var now = DateTime.UtcNow;

        async Task<int> Insert(DateTime start, DateTime created, ReservationStatus status = ReservationStatus.Confirmed)
        {
            await using var scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var r = new Reservation
            {
                ClientId = clientId,
                EmployeeId = kerimId,
                StartUtc = start,
                EndUtc = start.AddMinutes(30),
                Status = status,
                TotalPrice = 10,
                TotalDurationMinutes = 30,
                CreatedAtUtc = created,
            };
            db.Reservations.Add(r);
            await db.SaveChangesAsync();
            return r.Id;
        }

        var tomorrow = await Insert(now.AddHours(20), now.AddDays(-5));
        var cancelled = await Insert(now.AddHours(20), now.AddDays(-5), ReservationStatus.Cancelled);
        var bookedToday = await Insert(now.AddHours(20), now.AddHours(-1));
        var farAway = await Insert(now.AddDays(3), now.AddDays(-5));

        async Task<int> Run()
        {
            await using var scope = api.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ReminderService>().EnqueueDueAsync(default);
        }

        (await Run()).Should().BeGreaterThanOrEqualTo(1);
        (await Run()).Should().Be(0, "running again must not queue the same reminders");

        await using var check = api.Services.CreateAsyncScope();
        var db2 = check.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var reminded = await db2.ReservationReminders.Select(x => new { x.ReservationId, x.Kind }).ToListAsync();
        reminded.Should().Contain(x => x.ReservationId == tomorrow && x.Kind == ReminderKind.Day);
        reminded.Should().NotContain(x => x.ReservationId == cancelled);
        reminded.Should().NotContain(x => x.ReservationId == bookedToday);
        reminded.Should().NotContain(x => x.ReservationId == farAway);
        (await db2.OutboxMessages.CountAsync(m => m.Type == "ReservationReminderDue" && m.PayloadJson.Contains($"\"ReservationId\":{tomorrow},")))
            .Should().Be(1);

        // an hour before the visit the second reminder becomes due
        await db2.Reservations.Where(r => r.Id == tomorrow)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.StartUtc, now.AddMinutes(60)).SetProperty(r => r.EndUtc, now.AddMinutes(90)));
        (await Run()).Should().BeGreaterThanOrEqualTo(1);
        (await db2.ReservationReminders.CountAsync(x => x.ReservationId == tomorrow)).Should().Be(2);
    }

    [Fact]
    public async Task A_reminder_event_becomes_a_mail_to_the_client()
    {
        var (clientId, email, client) = await NewClient();
        var (kerimId, _, _) = await Kerim();
        var service = (await client.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Single(s => s.Name == "Šišanje").Id;
        var start = LocalTime.ToUtc(Days.Next(), new TimeOnly(13, 0), Zone);
        var reservation = (await (await client.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest(kerimId, [service], start, null, null)))
            .Content.ReadFromJsonAsync<ReservationDto>())!;

        // hand-built event: the scheduler's timing is covered above
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            scope.ServiceProvider.GetRequiredService<IOutbox>().Enqueue(new ReservationReminderDue(reservation.Id, clientId, kerimId, start, "Day"));
            await db.SaveChangesAsync();
        }
        var evt = await OutboxOf("ReservationReminderDue", p => p.Contains($"\"ReservationId\":{reservation.Id},"));
        await Process(evt);

        Mail.Sent.Should().Contain(m => m.To == email && m.Subject == "Podsjetnik: termin sutra");
        (await Inbox(client)).Should().Contain(n => n.Kind == "Reminder");
    }
}
