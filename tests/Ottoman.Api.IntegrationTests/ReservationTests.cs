using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Auth;
using Ottoman.Application.Catalog;
using Ottoman.Application.Common;
using Ottoman.Application.Employees;
using Ottoman.Application.Reservations;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Domain.Scheduling;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

/// <summary>Hands out a distinct future weekday per test so tests sharing one database never collide.</summary>
internal static class Days
{
    private static readonly object Gate = new();
    private static DateOnly _next = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);

    public static DateOnly Next()
    {
        lock (Gate)
        {
            while (_next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ||
                   // never land on a DST change week: the offset would differ from what the tests assume
                   (_next.Month is 3 or 10 && _next.Day >= 24))
                _next = _next.AddDays(1);
            var day = _next;
            _next = _next.AddDays(1);
            return day;
        }
    }
}

[Collection("api")]
public class ReservationTests(ApiFixture api)
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sarajevo");

    private static DateTime At(DateOnly day, int hour, int minute = 0) =>
        LocalTime.ToUtc(day, new TimeOnly(hour, minute), Zone);

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<(string Id, HttpClient Http)> Employee(string email)
    {
        var auth = await api.CreateClient().LoginOk(email, ApiFixture.DemoPassword);
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<(string Id, HttpClient Http, string Email)> NewClient()
    {
        var (email, auth) = await api.CreateClient().NewClient();
        return (auth.User.Id, api.CreateClient().As(auth), email);
    }

    private async Task<int> ServiceId(string name)
    {
        var admin = await Admin();
        return (await admin.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Single(s => s.Name == name).Id;
    }

    private async Task<string> KerimId() => (await Employee("kerim@ottoman.local")).Id;

    private static Task<HttpResponseMessage> Book(HttpClient http, string employeeId, DateTime start, int[] services, string? note = null, string? clientId = null) =>
        http.PostAsJsonAsync("/api/v1/reservations", new CreateReservationRequest(employeeId, services, start, note, clientId));

    private static async Task<ReservationDto> Dto(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<ReservationDto>())!;

    // ------------------------------------------------------------- happy path

    [Fact]
    public async Task Client_books_a_free_slot_and_the_server_sets_price_duration_and_status()
    {
        var (clientId, client, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var brada = await ServiceId("Uređivanje brade");

        var res = await Book(client, kerim, At(day, 10), [sisanje, brada], "Fade, molim");

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var r = await Dto(res);
        r.Status.Should().Be("Confirmed");
        r.ClientId.Should().Be(clientId);
        r.TotalPrice.Should().Be(40m);            // 25 + 15, computed by the server
        r.TotalDurationMinutes.Should().Be(60);
        r.EndUtc.Should().Be(At(day, 11));
        r.LocalStart.Should().Be($"{day:yyyy-MM-dd} 10:00");
        r.Items.Select(i => i.Name).Should().BeEquivalentTo("Šišanje", "Uređivanje brade");
        r.Note.Should().Be("Fade, molim");
        r.EmployeeName.Should().Be("Kerim Begić");
    }

    [Fact]
    public async Task A_booked_slot_disappears_from_availability_and_a_second_booking_gets_409()
    {
        var (_, first, _) = await NewClient();
        var (_, second, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");

        (await Book(first, kerim, At(day, 10), [sisanje])).StatusCode.Should().Be(HttpStatusCode.Created);

        var overlap = await Book(second, kerim, At(day, 10, 15), [sisanje]);
        overlap.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var times = (await second.GetFromJsonAsync<Ottoman.Application.Scheduling.AvailabilityDto>(
            $"/api/v1/employees/{kerim}/availability?date={day:yyyy-MM-dd}&serviceIds={sisanje}&allTimes=true"))!;
        times.Starts.Should().NotContain(At(day, 10));
        times.Starts.Should().NotContain(At(day, 10, 15));
        times.Starts.Should().Contain(At(day, 9, 30));   // ends exactly when the booking starts
        times.Starts.Should().Contain(At(day, 10, 30));  // starts exactly when it ends
    }

    // ------------------------------------------------- the race the DB must win

    [Fact]
    public async Task Twenty_simultaneous_bookings_of_the_same_slot_produce_exactly_one_winner()
    {
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var clients = new List<HttpClient>();
        for (var i = 0; i < 20; i++) clients.Add((await NewClient()).Http);

        var results = await Task.WhenAll(clients.Select(c => Book(c, kerim, At(day, 14), [sisanje])));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(19);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var dayRange = LocalTime.DayRangeUtc(day, Zone);
        (await db.Reservations.CountAsync(r => r.EmployeeId == kerim && r.StartUtc >= dayRange.Start && r.StartUtc < dayRange.End))
            .Should().Be(1);
    }

    [Fact]
    public async Task Simultaneous_overlapping_but_different_starts_still_allow_only_one_booking()
    {
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var starts = new[] { At(day, 15, 0), At(day, 15, 5), At(day, 15, 10), At(day, 15, 15), At(day, 15, 25) }; // all overlap
        var tasks = new List<Task<HttpResponseMessage>>();
        foreach (var start in starts.Concat(starts))
            tasks.Add(Book((await NewClient()).Http, kerim, start, [sisanje]));

        var results = await Task.WhenAll(tasks);

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
    }

    // ------------------------------------------------------- server-side rules

    [Fact]
    public async Task The_client_cannot_book_times_the_server_does_not_offer()
    {
        var (_, client, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");

        // Before opening, during the lunch break, overlapping the close, off the 5-minute grid.
        foreach (var bad in new[] { At(day, 8, 30), At(day, 12, 0), At(day, 12, 10), At(day, 16, 45), At(day, 10, 3) })
            (await Book(client, kerim, bad, [sisanje])).StatusCode.Should().Be(HttpStatusCode.Conflict, $"{bad:O}");
    }

    [Fact]
    public async Task Start_time_must_be_utc_and_services_must_belong_to_the_employee()
    {
        var (_, client, _) = await NewClient();
        var kerim = await KerimId();
        var tarik = (await Employee("tarik@ottoman.local")).Id;
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var onlyKerim = await ServiceId("Srednja / duga kosa");

        var unspecified = DateTime.SpecifyKind(At(day, 10), DateTimeKind.Unspecified);
        // JSON without a "Z" deserialises as Unspecified and must be refused rather than guessed at.
        var raw = await client.PostAsync("/api/v1/reservations", new StringContent(
            $"{{\"employeeId\":\"{kerim}\",\"serviceIds\":[{sisanje}],\"startUtc\":\"{unspecified:yyyy-MM-ddTHH:mm:ss}\"}}",
            System.Text.Encoding.UTF8, "application/json"));
        raw.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await Book(client, tarik, At(day, 10), [onlyKerim])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Book(client, kerim, At(day, 10), [])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Book(client, "nobody", At(day, 10), [sisanje])).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Booking_in_the_past_or_beyond_the_horizon_is_rejected()
    {
        var (_, client, _) = await NewClient();
        var kerim = await KerimId();
        var sisanje = await ServiceId("Šišanje");

        var past = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
        var far = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(120);
        while (past.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) past = past.AddDays(-1);
        while (far.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) far = far.AddDays(1);

        (await Book(client, kerim, At(past, 10), [sisanje])).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Book(client, kerim, At(far, 10), [sisanje])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_client_cannot_hold_more_than_five_active_reservations()
    {
        var (_, client, _) = await NewClient();
        var kerim = await KerimId();
        var sisanje = await ServiceId("Šišanje");

        for (var i = 0; i < 5; i++)
            (await Book(client, kerim, At(Days.Next(), 10), [sisanje])).StatusCode.Should().Be(HttpStatusCode.Created);

        var sixth = await Book(client, kerim, At(Days.Next(), 10), [sisanje]);
        sixth.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_clients_body_cannot_book_on_behalf_of_somebody_else()
    {
        var (victimId, _, _) = await NewClient();
        var (attackerId, attacker, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");

        var res = await Book(attacker, kerim, At(day, 10), [sisanje], clientId: victimId);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Dto(res)).ClientId.Should().Be(attackerId); // the id from the request body is ignored for clients
    }

    [Fact]
    public async Task The_price_is_a_snapshot_and_survives_later_catalogue_changes()
    {
        var admin = await Admin();
        var (_, client, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var name = $"Snap {Guid.NewGuid():N}"[..18];
        var created = (await (await admin.PostAsJsonAsync("/api/v1/services", new ServiceUpsertRequest(name, null, 30m, 30)))
            .Content.ReadFromJsonAsync<ServiceDto>())!;
        await admin.PutAsJsonAsync($"/api/v1/employees/{kerim}/services",
            new SetEmployeeServicesRequest([.. (await admin.GetFromJsonAsync<EmployeeDto>($"/api/v1/employees/{kerim}"))!.ServiceIds, created.Id]));

        var booked = await Dto(await Book(client, kerim, At(day, 10), [created.Id]));
        await admin.PutAsJsonAsync($"/api/v1/services/{created.Id}", new ServiceUpsertRequest(name, null, 99m, 60));

        var again = (await client.GetFromJsonAsync<ReservationDto>($"/api/v1/reservations/{booked.Id}"))!;
        again.TotalPrice.Should().Be(30m);
        again.TotalDurationMinutes.Should().Be(30);
        again.Items.Single().Price.Should().Be(30m);
    }

    // ---------------------------------------------------------------- walk-ins

    [Fact]
    public async Task Admin_books_on_behalf_of_a_client_and_must_name_one()
    {
        var admin = await Admin();
        var (clientId, client, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");

        (await Book(admin, kerim, At(day, 9), [sisanje])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Book(admin, kerim, At(day, 9), [sisanje], clientId: "not-a-user")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Book(admin, kerim, At(day, 9), [sisanje], clientId: kerim)).StatusCode.Should().Be(HttpStatusCode.BadRequest); // an employee is not a client

        var ok = await Book(admin, kerim, At(day, 9), [sisanje], clientId: clientId);
        ok.StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.GetFromJsonAsync<PagedResult<ReservationDto>>("/api/v1/reservations?upcoming=true"))!
            .Items.Should().ContainSingle(r => r.Id == ok.Content.ReadFromJsonAsync<ReservationDto>().Result!.Id);
    }

    [Fact]
    public async Task Employees_cannot_create_reservations_through_the_client_endpoint()
    {
        var (kerimId, kerim) = await Employee("kerim@ottoman.local");
        var sisanje = await ServiceId("Šišanje");

        (await Book(kerim, kerimId, At(Days.Next(), 10), [sisanje])).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------ visibility

    [Fact]
    public async Task Reservations_are_only_visible_to_the_owner_the_assigned_employee_and_admins()
    {
        var (_, owner, _) = await NewClient();
        var (_, stranger, _) = await NewClient();
        var (kerimId, kerim) = await Employee("kerim@ottoman.local");
        var (_, tarik) = await Employee("tarik@ottoman.local");
        var admin = await Admin();
        var sisanje = await ServiceId("Šišanje");
        var booked = await Dto(await Book(owner, kerimId, At(Days.Next(), 10), [sisanje]));
        var url = $"/api/v1/reservations/{booked.Id}";

        (await owner.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await kerim.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await stranger.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound); // 404, not 403: does not confirm it exists
        (await tarik.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await api.CreateClient().GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Lists_are_scoped_to_the_caller_even_when_filters_try_to_widen_them()
    {
        var (aId, a, _) = await NewClient();
        var (_, b, _) = await NewClient();
        var kerim = await KerimId();
        var sisanje = await ServiceId("Šišanje");
        var mine = await Dto(await Book(a, kerim, At(Days.Next(), 10), [sisanje]));
        var theirs = await Dto(await Book(b, kerim, At(Days.Next(), 10), [sisanje]));

        var list = (await a.GetFromJsonAsync<PagedResult<ReservationDto>>($"/api/v1/reservations?clientId={theirs.ClientId}&employeeId={kerim}"))!;

        list.Items.Should().OnlyContain(r => r.ClientId == aId);
        list.Items.Should().Contain(r => r.Id == mine.Id);
        list.Items.Should().NotContain(r => r.Id == theirs.Id);
    }

    [Fact]
    public async Task An_employee_sees_only_their_own_schedule()
    {
        var (_, client, _) = await NewClient();
        var (kerimId, kerim) = await Employee("kerim@ottoman.local");
        var (tarikId, tarik) = await Employee("tarik@ottoman.local");
        var sisanje = await ServiceId("Šišanje");
        var forKerim = await Dto(await Book(client, kerimId, At(Days.Next(), 10), [sisanje]));
        var forTarik = await Dto(await Book(client, tarikId, At(Days.Next(), 10), [sisanje]));

        var kerimList = (await kerim.GetFromJsonAsync<PagedResult<ReservationDto>>("/api/v1/reservations?pageSize=100"))!;
        var tarikList = (await tarik.GetFromJsonAsync<PagedResult<ReservationDto>>($"/api/v1/reservations?employeeId={kerimId}&pageSize=100"))!;

        kerimList.Items.Should().OnlyContain(r => r.EmployeeId == kerimId).And.Contain(r => r.Id == forKerim.Id);
        tarikList.Items.Should().OnlyContain(r => r.EmployeeId == tarikId).And.Contain(r => r.Id == forTarik.Id);
    }

    // ------------------------------------------------------------ cancelling

    [Fact]
    public async Task Client_cancels_and_the_slot_becomes_bookable_again()
    {
        var (_, first, _) = await NewClient();
        var (_, second, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var booked = await Dto(await Book(first, kerim, At(day, 10), [sisanje]));
        booked.CanCancel.Should().BeTrue();

        var cancel = await first.PostAsJsonAsync($"/api/v1/reservations/{booked.Id}/cancel", new CancelReservationRequest("Promjena planova"));

        cancel.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await Dto(cancel);
        cancelled.Status.Should().Be("Cancelled");
        cancelled.CancelReason.Should().Be("Promjena planova");
        (await Book(second, kerim, At(day, 10), [sisanje])).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_client_cannot_cancel_inside_the_cancellation_window_or_twice()
    {
        var (clientId, client, _) = await NewClient();
        var kerim = await KerimId();
        int soon;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            var start = DateTime.UtcNow.AddMinutes(30);
            var r = new Reservation
            {
                ClientId = clientId,
                EmployeeId = kerim,
                StartUtc = start,
                EndUtc = start.AddMinutes(30),
                Status = ReservationStatus.Confirmed,
                CreatedAtUtc = DateTime.UtcNow,
                TotalPrice = 25m,
                TotalDurationMinutes = 30,
            };
            db.Reservations.Add(r);
            await db.SaveChangesAsync();
            soon = r.Id;
        }

        var res = await client.PostAsJsonAsync($"/api/v1/reservations/{soon}/cancel", new CancelReservationRequest(null));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).Should().Contain("otkazati");
        (await Dto(await client.GetAsync($"/api/v1/reservations/{soon}"))).CanCancel.Should().BeFalse();

        // Admin can still cancel it; a second cancel is then refused for everyone.
        var admin = await Admin();
        (await admin.PostAsJsonAsync($"/api/v1/reservations/{soon}/cancel", new CancelReservationRequest(null))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsJsonAsync($"/api/v1/reservations/{soon}/cancel", new CancelReservationRequest(null))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Another_client_cannot_cancel_someone_elses_reservation()
    {
        var (_, owner, _) = await NewClient();
        var (_, stranger, _) = await NewClient();
        var kerim = await KerimId();
        var sisanje = await ServiceId("Šišanje");
        var booked = await Dto(await Book(owner, kerim, At(Days.Next(), 10), [sisanje]));

        (await stranger.PostAsJsonAsync($"/api/v1/reservations/{booked.Id}/cancel", new CancelReservationRequest(null)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Dto(await owner.GetAsync($"/api/v1/reservations/{booked.Id}"))).Status.Should().Be("Confirmed");
    }

    // ---------------------------------------------------------------- finishing

    [Fact]
    public async Task Staff_complete_a_started_appointment_but_not_a_future_one_and_clients_never()
    {
        var (clientId, client, _) = await NewClient();
        var (kerimId, kerim) = await Employee("kerim@ottoman.local");
        var (_, tarik) = await Employee("tarik@ottoman.local");
        int started, future;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            Reservation Make(DateTime start) => new()
            {
                ClientId = clientId,
                EmployeeId = kerimId,
                StartUtc = start,
                EndUtc = start.AddMinutes(30),
                Status = ReservationStatus.Confirmed,
                CreatedAtUtc = DateTime.UtcNow,
                TotalPrice = 25m,
                TotalDurationMinutes = 30,
            };
            var a = Make(DateTime.UtcNow.AddMinutes(-45));
            var b = Make(DateTime.UtcNow.AddDays(3));
            db.Reservations.AddRange(a, b);
            await db.SaveChangesAsync();
            (started, future) = (a.Id, b.Id);
        }

        (await client.PostAsync($"/api/v1/reservations/{started}/complete", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await tarik.PostAsync($"/api/v1/reservations/{started}/complete", null)).StatusCode.Should().Be(HttpStatusCode.NotFound); // not their reservation
        (await kerim.PostAsync($"/api/v1/reservations/{future}/complete", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var done = await kerim.PostAsync($"/api/v1/reservations/{started}/complete", null);
        done.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Dto(done)).Status.Should().Be("Completed");
        (await kerim.PostAsync($"/api/v1/reservations/{started}/no-show", null)).StatusCode.Should().Be(HttpStatusCode.Conflict); // already closed
    }

    // -------------------------------------------------------------- reschedule

    [Fact]
    public async Task Admin_reschedules_to_a_free_slot_and_the_old_time_is_released()
    {
        var admin = await Admin();
        var (_, client, _) = await NewClient();
        var (_, other, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var booked = await Dto(await Book(client, kerim, At(day, 10), [sisanje]));

        // Moving 15 minutes later overlaps its own old steps: must not collide with itself.
        var moved = await admin.PutAsJsonAsync($"/api/v1/reservations/{booked.Id}/reschedule", new RescheduleReservationRequest(At(day, 10, 15)));

        moved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Dto(moved)).StartUtc.Should().Be(At(day, 10, 15));
        (await Book(other, kerim, At(day, 9, 30), [sisanje])).StatusCode.Should().Be(HttpStatusCode.Created);   // freed time before
        (await Book(other, kerim, At(day, 10, 30), [sisanje])).StatusCode.Should().Be(HttpStatusCode.Conflict); // new time is held
    }

    [Fact]
    public async Task Rescheduling_onto_a_taken_slot_conflicts_and_changes_nothing()
    {
        var admin = await Admin();
        var (_, a, _) = await NewClient();
        var (_, b, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var first = await Dto(await Book(a, kerim, At(day, 10), [sisanje]));
        var second = await Dto(await Book(b, kerim, At(day, 13), [sisanje]));

        var res = await admin.PutAsJsonAsync($"/api/v1/reservations/{second.Id}/reschedule", new RescheduleReservationRequest(At(day, 10, 15)));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Dto(await b.GetAsync($"/api/v1/reservations/{second.Id}"))).StartUtc.Should().Be(At(day, 13));
        (await Dto(await a.GetAsync($"/api/v1/reservations/{first.Id}"))).StartUtc.Should().Be(At(day, 10));
    }

    [Fact]
    public async Task Only_admins_can_reschedule()
    {
        var (_, client, _) = await NewClient();
        var (kerimId, kerim) = await Employee("kerim@ottoman.local");
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var booked = await Dto(await Book(client, kerimId, At(day, 10), [sisanje]));
        var body = new RescheduleReservationRequest(At(day, 11));

        (await client.PutAsJsonAsync($"/api/v1/reservations/{booked.Id}/reschedule", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await kerim.PutAsJsonAsync($"/api/v1/reservations/{booked.Id}/reschedule", body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----------------------------------------------------------------- summary

    [Fact]
    public async Task Summary_counts_statuses_for_the_requested_period_and_scopes_employees()
    {
        var admin = await Admin();
        var (_, client, _) = await NewClient();
        var (kerimId, kerim) = await Employee("kerim@ottoman.local");
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        await Book(client, kerimId, At(day, 10), [sisanje]);
        var toCancel = await Dto(await Book(client, kerimId, At(day, 11), [sisanje]));
        await client.PostAsJsonAsync($"/api/v1/reservations/{toCancel.Id}/cancel", new CancelReservationRequest(null));
        var url = $"/api/v1/reservations/summary?fromDate={day:yyyy-MM-dd}&toDate={day:yyyy-MM-dd}";

        var summary = (await admin.GetFromJsonAsync<ReservationSummaryDto>(url))!;
        var asEmployee = (await kerim.GetFromJsonAsync<ReservationSummaryDto>(url))!;

        summary.Upcoming.Should().Be(1);
        summary.Cancelled.Should().Be(1);
        summary.TotalClients.Should().Be(1);
        asEmployee.Should().Be(summary); // all of that day's bookings are Kerim's
        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden); // clients have no dashboard
    }

    // ---------------------------------------------------------------- outbox

    [Fact]
    public async Task Booking_and_cancelling_emit_events_for_the_notification_pipeline()
    {
        var (_, client, _) = await NewClient();
        var kerim = await KerimId();
        var sisanje = await ServiceId("Šišanje");
        var booked = await Dto(await Book(client, kerim, At(Days.Next(), 10), [sisanje]));
        await client.PostAsJsonAsync($"/api/v1/reservations/{booked.Id}/cancel", new CancelReservationRequest(null));

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
        var json = await db.OutboxMessages.Where(m => m.PayloadJson.Contains($"\"ReservationId\":{booked.Id},")).Select(m => m.Type).ToListAsync();

        json.Should().Contain("ReservationCreated").And.Contain("ReservationCancelled");
    }

    [Fact]
    public async Task Date_filters_use_salon_local_days_and_include_both_ends()
    {
        var (_, client, _) = await NewClient();
        var (kerimId, kerim) = await Employee("kerim@ottoman.local");
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var booked = await Dto(await Book(client, kerimId, At(day, 9), [sisanje]));

        async Task<List<int>> Ids(DateOnly from, DateOnly to) =>
            (await kerim.GetFromJsonAsync<PagedResult<ReservationDto>>($"/api/v1/reservations?fromDate={from:yyyy-MM-dd}&toDate={to:yyyy-MM-dd}&pageSize=100"))!
                .Items.Select(r => r.Id).ToList();

        (await Ids(day, day)).Should().Contain(booked.Id);
        (await Ids(day.AddDays(-3), day)).Should().Contain(booked.Id);   // last day is inclusive
        (await Ids(day, day.AddDays(3))).Should().Contain(booked.Id);    // first day is inclusive
        (await Ids(day.AddDays(1), day.AddDays(5))).Should().NotContain(booked.Id);
        (await Ids(day.AddDays(-5), day.AddDays(-1))).Should().NotContain(booked.Id);
    }

    [Fact]
    public async Task Admin_can_see_a_reservations_own_time_as_free_when_moving_it_but_clients_cannot()
    {
        var admin = await Admin();
        var (_, client, _) = await NewClient();
        var kerim = await KerimId();
        var day = Days.Next();
        var sisanje = await ServiceId("Šišanje");
        var booked = await Dto(await Book(client, kerim, At(day, 10), [sisanje]));
        string Url(int? exclude) =>
            $"/api/v1/employees/{kerim}/availability?date={day:yyyy-MM-dd}&serviceIds={sisanje}&allTimes=true"
            + (exclude is null ? "" : $"&excludeReservationId={exclude}");

        var adminView = (await admin.GetFromJsonAsync<Ottoman.Application.Scheduling.AvailabilityDto>(Url(booked.Id)))!;
        var clientView = (await client.GetFromJsonAsync<Ottoman.Application.Scheduling.AvailabilityDto>(Url(booked.Id)))!;

        adminView.Starts.Should().Contain(At(day, 10, 15));    // overlaps only its own booking
        clientView.Starts.Should().NotContain(At(day, 10, 15)); // the parameter is ignored for non-admins
    }
}
