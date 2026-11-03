using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Api.Controllers;
using Ottoman.Application.Notifications;
using Ottoman.Application.Push;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Notifications;
using Ottoman.Infrastructure.Persistence;
using Ottoman.Infrastructure.Push;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class PushTests(ApiFixture api)
{
    private FakePushSender Push => api.Services.GetRequiredService<FakePushSender>();

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, string Email, HttpClient Http)> NewClient()
    {
        var (email, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, email, api.CreateClient().As(auth));
    }

    private static string NewToken() => $"fcm_{Guid.NewGuid():N}:APA91b{Guid.NewGuid():N}";

    private Task<HttpResponseMessage> Register(HttpClient http, string token, string platform = "android") =>
        http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(token, platform));

    private async Task<List<DeviceToken>> Rows(Func<DeviceToken, bool> where)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        return (await db.DeviceTokens.AsNoTracking().ToListAsync()).Where(where).ToList();
    }

    private async Task Process(string type, Func<string, bool> payload)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var all = await db.OutboxMessages.AsNoTracking().Where(m => m.Type == type).OrderByDescending(m => m.Id).Take(200).ToListAsync();
        var m = all.First(x => payload(x.PayloadJson));
        await scope.ServiceProvider.GetRequiredService<EventProcessor>().ProcessAsync(m.Id, m.Type, m.PayloadJson, default);
    }

    private async Task<int> Announce(HttpClient admin, string title, string userId)
    {
        var res = await admin.PostAsJsonAsync("/api/v1/announcements", new AnnouncementRequest(title, "Zdravo", AnnouncementAudience.User, userId));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<AnnouncementDto>())!.Id;
    }

    // ---------------------------------------------------------------- registration

    [Fact]
    public async Task Registering_a_phone_needs_a_login_and_a_sane_token()
    {
        (await Register(api.CreateClient(), NewToken())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var (_, _, client) = await NewClient();
        (await Register(client, "")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Register(client, "has spaces <script>")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Register(client, new string('a', 513))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Register(client, NewToken(), "windows")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Changing_the_password_forgets_every_phone_of_that_account_only()
    {
        var (id, _, client) = await NewClient();
        var (otherId, _, other) = await NewClient();
        (await Register(client, NewToken())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Register(other, NewToken())).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.PostAsJsonAsync("/api/v1/me/change-password",
            new Ottoman.Application.Auth.ChangePasswordRequest(TestClient.StrongPassword, "Changed_Pass1!")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Rows(d => d.UserId == id)).Should().BeEmpty("a new password ends every session, push included");
        (await Rows(d => d.UserId == otherId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task A_phone_is_remembered_once_and_moves_to_whoever_signed_in_last()
    {
        var (aId, _, a) = await NewClient();
        var (bId, _, b) = await NewClient();
        var token = NewToken();

        (await Register(a, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Register(a, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Rows(d => d.Token == token)).Should().ContainSingle().Which.UserId.Should().Be(aId);

        (await Register(b, token)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Rows(d => d.Token == token)).Should().ContainSingle().Which.UserId.Should().Be(bId);
    }

    [Fact]
    public async Task Only_the_owner_can_forget_a_phone()
    {
        var (_, _, a) = await NewClient();
        var (_, _, b) = await NewClient();
        var token = NewToken();
        await Register(a, token);

        (await b.PostAsJsonAsync("/api/v1/devices/unregister", new UnregisterDeviceRequest(token))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await Rows(d => d.Token == token)).Should().HaveCount(1, "someone else's sign-out must not remove my phone");

        (await a.PostAsJsonAsync("/api/v1/devices/unregister", new UnregisterDeviceRequest(token))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await Rows(d => d.Token == token)).Should().BeEmpty();
    }

    [Fact]
    public async Task An_account_keeps_only_its_newest_phones()
    {
        var (id, _, client) = await NewClient();
        var tokens = Enumerable.Range(0, 12).Select(_ => NewToken()).ToList();
        foreach (var t in tokens) (await Register(client, t)).EnsureSuccessStatusCode();

        var kept = (await Rows(d => d.UserId == id)).Select(d => d.Token).ToList();
        kept.Should().HaveCount(10);
        kept.Should().Contain(tokens.Skip(2), "the newest ten are kept");
        kept.Should().NotContain(tokens[0]);
    }

    // ---------------------------------------------------------------- delivery

    [Fact]
    public async Task A_message_for_a_user_is_pushed_to_each_of_their_phones_and_only_theirs()
    {
        var (aId, _, a) = await NewClient();
        var (_, _, b) = await NewClient();
        var admin = await Admin();
        var phone1 = NewToken();
        var phone2 = NewToken();
        var other = NewToken();
        await Register(a, phone1);
        await Register(a, phone2, "ios");
        await Register(b, other);
        var title = $"Push {Guid.NewGuid():N}";

        var id = await Announce(admin, title, aId);
        await Process("AnnouncementPublished", p => p.Contains($"\"AnnouncementId\":{id}") || p.Contains($"\"announcementId\":{id}"));

        var sent = Push.Sent.Where(s => s.Message.Title == title).ToList();
        sent.Select(s => s.Token).Should().BeEquivalentTo([phone1, phone2]);
        sent.Should().OnlyContain(s => s.Message.Kind == "Announcement");
        Push.Sent.Should().NotContain(s => s.Token == other && s.Message.Title == title);
    }

    [Fact]
    public async Task A_dead_token_is_forgotten_and_does_not_stop_the_others()
    {
        var (aId, _, a) = await NewClient();
        var admin = await Admin();
        var dead = NewToken();
        var alive = NewToken();
        await Register(a, dead);
        await Register(a, alive);
        Push.DeadTokens.Add(dead);
        var title = $"Push {Guid.NewGuid():N}";

        var id = await Announce(admin, title, aId);
        await Process("AnnouncementPublished", p => p.Contains($":{id}"));

        Push.Sent.Should().Contain(s => s.Token == alive && s.Message.Title == title);
        (await Rows(d => d.Token == dead)).Should().BeEmpty();
        (await Rows(d => d.Token == alive)).Should().HaveCount(1);
    }

    [Fact]
    public async Task A_password_reset_code_never_goes_to_a_lock_screen()
    {
        var (_, email, client) = await NewClient();
        var token = NewToken();
        await Register(client, token);

        (await api.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).EnsureSuccessStatusCode();
        await Process("PasswordResetRequested", p => p.Contains(email));

        Push.Sent.Should().NotContain(s => s.Token == token);
    }

    [Fact]
    public void Lock_screen_policy_keeps_security_mail_out_and_text_short()
    {
        PushPolicy.ShouldPush("PasswordReset").Should().BeFalse();
        PushPolicy.ShouldPush("RegistrationAttempt").Should().BeFalse();
        PushPolicy.ShouldPush("ReservationCreated").Should().BeTrue();
        PushPolicy.Preview("a\nb\r\nc").Should().Be("a b c");
        PushPolicy.Preview(new string('x', 400)).Should().HaveLength(160).And.EndWith("…");
    }

    [Theory]
    [InlineData(404, "{}", PushOutcome.InvalidToken)]
    [InlineData(400, "{\"error\":{\"message\":\"The registration token is not a valid FCM registration token\"}}", PushOutcome.InvalidToken)]
    [InlineData(400, "{\"error\":{\"details\":[{\"errorCode\":\"UNREGISTERED\"}]}}", PushOutcome.InvalidToken)]
    [InlineData(401, "{}", PushOutcome.Failed)]
    [InlineData(403, "{}", PushOutcome.Failed)]
    [InlineData(500, "{}", PushOutcome.Failed)]
    [InlineData(400, "{\"error\":{\"message\":\"Invalid JSON payload\"}}", PushOutcome.Failed)]
    public void Firebase_answers_are_classified_so_only_dead_tokens_are_forgotten(int status, string body, PushOutcome expected) =>
        FcmPushSender.Classify(status, body).Should().Be(expected);
}
