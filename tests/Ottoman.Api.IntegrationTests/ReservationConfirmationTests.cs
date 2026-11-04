using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Catalog;
using Ottoman.Application.Reservations;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Pending -> Confirmed: automatic by default, by hand when the salon switches auto-confirm off.</summary>
[Collection("api")]
public class ReservationConfirmationTests(ApiFixture api)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static DateTime At(DateOnly day, int hour) => LocalTime.ToUtc(day, new TimeOnly(hour, 0), Zone);

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, HttpClient Http)> Staff(string email)
    {
        var auth = await api.CreateClient().LoginOk(email, ApiFixture.DemoPassword);
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<HttpClient> NewClient() => api.CreateClient().As((await api.CreateClient().NewClient()).Auth);

    private async Task<int> Sisanje() =>
        (await (await Admin()).GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Single(s => s.Name == "Šišanje").Id;

    private static async Task<ReservationDto> Booked(HttpResponseMessage res)
    {
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.Content.ReadFromJsonAsync<ReservationDto>())!;
    }

    private Task<HttpResponseMessage> Book(HttpClient http, string employeeId, DateTime start, int service) =>
        http.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest(employeeId, [service], start, null));

    private async Task SetAutoConfirm(bool on)
    {
        await using var scope = api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OttomanDbContext>().SalonSettings
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.AutoConfirmReservations, on));
    }

    [Fact]
    public async Task By_default_the_system_confirms_a_booking_at_once_and_the_audit_says_so()
    {
        var (kerim, _) = await Staff("kerim@ottoman.local");
        var dto = await Booked(await Book(await NewClient(), kerim, At(Days.Next(), 10), await Sisanje()));

        dto.Status.Should().Be("Confirmed");
        dto.ConfirmedBy.Should().Be("System");
        dto.ConfirmedAtUtc.Should().NotBeNull();

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        (await db.AuditEntries.AnyAsync(a => a.Role == "System" && a.UserId == null && a.Target == $"id={dto.Id}"))
            .Should().BeTrue("the system's transition is audited");
        (await db.OutboxMessages.AnyAsync(m => m.Type == "ReservationCreated" && m.PayloadJson.Contains($"\"ReservationId\":{dto.Id},") && m.PayloadJson.Contains("\"Confirmed\":true")))
            .Should().BeTrue();
    }

    [Fact]
    public async Task With_auto_confirm_off_a_booking_waits_holds_its_time_and_the_barber_confirms_or_declines_it()
    {
        await SetAutoConfirm(false);
        try
        {
            var (kerim, kerimHttp) = await Staff("kerim@ottoman.local");
            var (_, tarikHttp) = await Staff("tarik@ottoman.local");
            var service = await Sisanje();
            var day = Days.Next();
            var client = await NewClient();

            var first = await Booked(await Book(client, kerim, At(day, 10), service));
            first.Status.Should().Be("Pending");
            first.ConfirmedBy.Should().BeNull();
            (await Book(await NewClient(), kerim, At(day, 10), service)).StatusCode.Should().Be(HttpStatusCode.Conflict, "a pending booking holds its time");

            // Only staff confirm, and a barber only his own bookings.
            (await client.PostAsync($"/api/v1/reservations/{first.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await tarikHttp.PostAsync($"/api/v1/reservations/{first.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

            var mine = (await kerimHttp.GetFromJsonAsync<ReservationDto>($"/api/v1/reservations/{first.Id}"))!;
            mine.CanConfirm.Should().BeTrue();
            mine.CanDecline.Should().BeTrue();

            var confirmed = await kerimHttp.PostAsync($"/api/v1/reservations/{first.Id}/confirm", null);
            confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
            var c = (await confirmed.Content.ReadFromJsonAsync<ReservationDto>())!;
            c.Status.Should().Be("Confirmed");
            c.ConfirmedBy.Should().Be("Employee");
            (await kerimHttp.PostAsync($"/api/v1/reservations/{first.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await kerimHttp.PostAsJsonAsync($"/api/v1/reservations/{first.Id}/decline", new DeclineReservationRequest("Prekasno")))
                .StatusCode.Should().Be(HttpStatusCode.Conflict, "a confirmed booking is cancelled, not declined");

            // Declining needs a reason, frees the time and tells the client why.
            var second = await Booked(await Book(client, kerim, At(day, 13), service));
            (await kerimHttp.PostAsJsonAsync($"/api/v1/reservations/{second.Id}/decline", new DeclineReservationRequest("")))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var declined = await kerimHttp.PostAsJsonAsync($"/api/v1/reservations/{second.Id}/decline", new DeclineReservationRequest("Frizer je na godišnjem"));
            declined.StatusCode.Should().Be(HttpStatusCode.OK);
            var d = (await declined.Content.ReadFromJsonAsync<ReservationDto>())!;
            d.Status.Should().Be("Cancelled");
            d.Declined.Should().BeTrue();
            d.CancelReason.Should().Be("Frizer je na godišnjem");
            (await Book(await NewClient(), kerim, At(day, 13), service)).StatusCode.Should().Be(HttpStatusCode.Created, "the time is free again");

            await using var scope = api.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            (await db.OutboxMessages.AnyAsync(m => m.Type == "ReservationConfirmed" && m.PayloadJson.Contains($"\"ReservationId\":{first.Id},")))
                .Should().BeTrue();
            (await db.OutboxMessages.AnyAsync(m => m.Type == "ReservationCancelled" && m.PayloadJson.Contains($"\"ReservationId\":{second.Id},") && m.PayloadJson.Contains("\"Declined\":true")))
                .Should().BeTrue();
        }
        finally
        {
            await SetAutoConfirm(true);
        }
    }

    [Fact]
    public async Task The_salon_page_shows_and_changes_the_auto_confirm_setting()
    {
        var admin = await Admin();
        var profile = await admin.GetFromJsonAsync<Ottoman.Application.Salon.SalonProfileDto>("/api/v1/salon");
        profile!.AutoConfirmReservations.Should().BeTrue();
        try
        {
            var body = new Ottoman.Application.Salon.UpdateSalonProfileRequest(
                profile.Name, profile.Description, profile.Address, profile.Phone, profile.MapsUrl, profile.Latitude, profile.Longitude,
                profile.Hours, profile.InstagramUrl, profile.FacebookUrl, AutoConfirmReservations: false);
            (await admin.PutAsJsonAsync("/api/v1/salon", body)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetFromJsonAsync<Ottoman.Application.Salon.SalonProfileDto>("/api/v1/salon"))!.AutoConfirmReservations.Should().BeFalse();

            // An app that does not know the setting leaves it alone.
            (await admin.PutAsJsonAsync("/api/v1/salon", body with { AutoConfirmReservations = null })).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetFromJsonAsync<Ottoman.Application.Salon.SalonProfileDto>("/api/v1/salon"))!.AutoConfirmReservations.Should().BeFalse();
        }
        finally
        {
            await SetAutoConfirm(true);
        }
    }
}
