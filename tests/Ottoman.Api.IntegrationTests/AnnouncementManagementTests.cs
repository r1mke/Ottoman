using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Common;
using Ottoman.Application.Notifications;
using Ottoman.Contracts;
using Ottoman.Domain;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

/// <summary>After sending, the admin edits, hides, dates and finds announcements; recipients see only live ones.</summary>
[Collection("api")]
public class AnnouncementManagementTests(ApiFixture api)
{
    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, HttpClient Http)> NewClient()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private static async Task<List<NotificationDto>> Inbox(HttpClient http) =>
        (await http.GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications?pageSize=100"))!.Items.ToList();

    private static async Task<int> Unread(HttpClient http) =>
        (await http.GetFromJsonAsync<UnreadCountDto>("/api/v1/notifications/unread-count"))!.Count;

    private static async Task<AnnouncementDto> Send(HttpClient admin, string userId, string title,
        AnnouncementCategory category = AnnouncementCategory.News, DateTime? expires = null)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/announcements",
            new AnnouncementRequest(title, "Tekst", AnnouncementAudience.User, userId, false, category, expires));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<AnnouncementDto>())!;
    }

    [Fact]
    public async Task Editing_changes_the_inbox_text_and_category_without_sending_again()
    {
        var admin = await Admin();
        var (clientId, client) = await NewClient();
        var sent = await Send(admin, clientId, "Akcija na vosak", AnnouncementCategory.Promotion);
        sent.Category.Should().Be(AnnouncementCategory.Promotion);
        (await Inbox(client)).Single().Category.Should().Be(AnnouncementCategory.Promotion);

        var edited = await admin.PutAsJsonAsync($"/api/v1/announcements/{sent.Id}",
            new UpdateAnnouncementRequest("Akcija na vosak do petka", "Novi tekst", AnnouncementCategory.Notice, DateTime.UtcNow.AddDays(3)));
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await edited.Content.ReadFromJsonAsync<AnnouncementDto>())!;
        (dto.Title, dto.Category, dto.UpdatedAtUtc is not null).Should().Be(("Akcija na vosak do petka", AnnouncementCategory.Notice, true));

        var row = (await Inbox(client)).Single();
        (row.Title, row.Body, row.Category).Should().Be(("Akcija na vosak do petka", "Novi tekst", AnnouncementCategory.Notice));

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.OutboxMessages.CountAsync(m => m.Type == nameof(AnnouncementPublished) && m.PayloadJson.Contains($"\"AnnouncementId\":{sent.Id}}}")))
            .Should().Be(1, "editing never publishes the announcement again");
        (await db.UserNotifications.CountAsync(n => n.AnnouncementId == sent.Id)).Should().Be(1);

        (await admin.PutAsJsonAsync("/api/v1/announcements/999999",
            new UpdateAnnouncementRequest("x", "y", AnnouncementCategory.News, null))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_hidden_announcement_disappears_for_recipients_and_comes_back_when_shown()
    {
        var admin = await Admin();
        var (clientId, client) = await NewClient();
        var sent = await Send(admin, clientId, "Zatvoreno u ponedjeljak");
        (await Unread(client)).Should().Be(1);

        var hidden = await admin.PutAsJsonAsync($"/api/v1/announcements/{sent.Id}/hide", new AnnouncementHideRequest(true));
        (await hidden.Content.ReadFromJsonAsync<AnnouncementDto>())!.IsHidden.Should().BeTrue();
        (await Inbox(client)).Should().BeEmpty();
        (await Unread(client)).Should().Be(0);
        (await admin.GetFromJsonAsync<AnnouncementDto>($"/api/v1/announcements/{sent.Id}"))!.IsHidden.Should().BeTrue("the admin still sees it");

        (await admin.PutAsJsonAsync($"/api/v1/announcements/{sent.Id}/hide", new AnnouncementHideRequest(false))).EnsureSuccessStatusCode();
        (await Inbox(client)).Should().ContainSingle(n => n.Title == "Zatvoreno u ponedjeljak");
        (await admin.PutAsJsonAsync($"/api/v1/announcements/{sent.Id}/hide", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Only the admin manages announcements.
        (await client.PutAsJsonAsync($"/api/v1/announcements/{sent.Id}/hide", new AnnouncementHideRequest(true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync($"/api/v1/announcements/{sent.Id}",
            new UpdateAnnouncementRequest("x", "y", AnnouncementCategory.News, null))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_expired_announcement_is_gone_for_recipients_and_an_end_date_in_the_past_is_refused()
    {
        var admin = await Admin();
        var (clientId, client) = await NewClient();
        (await admin.PostAsJsonAsync("/api/v1/announcements",
            new AnnouncementRequest("Prošlost", "Tekst", AnnouncementAudience.User, clientId, false, AnnouncementCategory.News, DateTime.UtcNow.AddHours(-1))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var sent = await Send(admin, clientId, "Kratka akcija", AnnouncementCategory.Promotion, DateTime.UtcNow.AddDays(1));
        (await Inbox(client)).Should().ContainSingle();
        (await admin.PutAsJsonAsync($"/api/v1/announcements/{sent.Id}",
            new UpdateAnnouncementRequest("Kratka akcija", "Tekst", AnnouncementCategory.Promotion, DateTime.UtcNow.AddMinutes(-5))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            await db.Announcements.Where(a => a.Id == sent.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
        }
        (await Inbox(client)).Should().BeEmpty();
        (await Unread(client)).Should().Be(0);
        (await admin.GetFromJsonAsync<AnnouncementDto>($"/api/v1/announcements/{sent.Id}"))!.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task The_list_is_searched_and_paged_and_the_picture_can_be_changed()
    {
        var admin = await Admin();
        var (clientId, _) = await NewClient();
        var word = Guid.NewGuid().ToString("N")[..10];
        var a = await Send(admin, clientId, $"Novost {word} 1");
        await Send(admin, clientId, $"Novost {word} 2");

        var found = (await admin.GetFromJsonAsync<PagedResult<AnnouncementDto>>($"/api/v1/announcements?search={word}&pageSize=1"))!;
        found.TotalCount.Should().Be(2);
        found.Items.Should().ContainSingle().Which.Title.Should().Be($"Novost {word} 2");

        var file = new ByteArrayContent(TestImages.Png(424242));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var withPicture = await admin.PutAsync($"/api/v1/announcements/{a.Id}/image", new MultipartFormDataContent { { file, "file", "a.png" } });
        withPicture.StatusCode.Should().Be(HttpStatusCode.OK);
        (await withPicture.Content.ReadFromJsonAsync<AnnouncementDto>())!.ImageUrl.Should().NotBeNull();
        var cleared = await admin.DeleteAsync($"/api/v1/announcements/{a.Id}/image");
        (await cleared.Content.ReadFromJsonAsync<AnnouncementDto>())!.ImageUrl.Should().BeNull();
    }
}
