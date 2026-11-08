using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Ottoman.Application.Notifications;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class AnnouncementImageTests(ApiFixture api)
{
    private static int _seq;

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private static MultipartFormDataContent Form(string title, byte[]? picture, string audience = "Clients")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "Title" },
            { new StringContent("Tekst vijesti"), "Body" },
            { new StringContent(audience), "Audience" },
            { new StringContent("false"), "SendEmail" },
        };
        if (picture is not null)
        {
            var file = new ByteArrayContent(picture);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "vijest.png");
        }
        return form;
    }

    private static byte[] Png()
    {
        return TestImages.Png(Interlocked.Increment(ref _seq));
    }

    [Fact]
    public async Task A_news_item_can_carry_a_picture_that_reaches_every_recipients_inbox()
    {
        var admin = await Admin();
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        var title = $"Vijest {Guid.NewGuid():N}";

        var res = await admin.PostAsync("/api/v1/announcements/with-image", Form(title, Png()));

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await res.Content.ReadFromJsonAsync<AnnouncementDto>())!;
        dto.ImageUrl.Should().Contain("ottoman/announcements/");
        dto.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(2));

        var inbox = (await client.GetFromJsonAsync<Ottoman.Application.Common.PagedResult<NotificationDto>>("/api/v1/notifications"))!.Items;
        inbox.Single(n => n.Title == title).ImageUrl.Should().Be(dto.ImageUrl);
        // The history for the admin shows the picture too.
        (await admin.GetFromJsonAsync<Ottoman.Application.Common.PagedResult<AnnouncementDto>>("/api/v1/announcements"))!
            .Items.First(a => a.Id == dto.Id).ImageUrl.Should().Be(dto.ImageUrl);
    }

    [Fact]
    public async Task A_news_item_without_a_picture_is_still_fine_and_a_bad_picture_is_refused()
    {
        var admin = await Admin();
        var title = $"Bez slike {Guid.NewGuid():N}";

        var plain = await admin.PostAsync("/api/v1/announcements/with-image", Form(title, null));
        plain.StatusCode.Should().Be(HttpStatusCode.Created);
        (await plain.Content.ReadFromJsonAsync<AnnouncementDto>())!.ImageUrl.Should().BeNull();

        // Not a picture: the bytes decide, not the name or the declared type.
        var notAPicture = await admin.PostAsync("/api/v1/announcements/with-image",
            Form($"Loše {Guid.NewGuid():N}", new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
        notAPicture.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_the_admin_sends_news_with_a_picture_and_the_text_is_still_validated()
    {
        var (_, auth) = await api.CreateClient().NewClient();
        var client = api.CreateClient().As(auth);
        (await client.PostAsync("/api/v1/announcements/with-image", Form("x", Png()))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await Admin();
        (await admin.PostAsync("/api/v1/announcements/with-image", Form("", Png()))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
