using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ottoman.Application.Auth;
using Ottoman.Application.Catalog;
using Ottoman.Application.Employees;
using Ottoman.Application.Scheduling;
using Ottoman.Application.Settings;
using Ottoman.Domain;
using Ottoman.Domain.Entities;
using Ottoman.Infrastructure.Persistence;

namespace Ottoman.Api.IntegrationTests;

[Collection("api")]
public class SchedulingTests(ApiFixture api)
{
    // 2030-01-07 is a Monday, 2030-01-08 a Tuesday, 2030-01-06 a Sunday. Winter: Sarajevo = UTC+1.
    private static readonly DateOnly Monday = new(2030, 1, 7);
    private static readonly DateOnly Tuesday = new(2030, 1, 8);
    private static readonly DateOnly Sunday = new(2030, 1, 6);

    private static DateTime Utc(DateOnly d, int localHour, int minute = 0) =>
        new(d.Year, d.Month, d.Day, localHour - 1, minute, 0, DateTimeKind.Utc);

    private async Task<HttpClient> Admin() =>
        api.CreateClient().As(await api.CreateClient().LoginOk(ApiFixture.AdminEmail, ApiFixture.AdminPassword));

    private async Task<HttpClient> Client() => api.CreateClient().As((await api.CreateClient().NewClient()).Auth);

    private async Task<(string Id, HttpClient Http)> Kerim()
    {
        var auth = await api.CreateClient().LoginOk("kerim@ottoman.local", ApiFixture.DemoPassword);
        return (auth.User.Id, api.CreateClient().As(auth));
    }

    private async Task<EmployeeDto> Employee(HttpClient http, string firstName) =>
        (await http.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!.Single(e => e.FirstName == firstName);

    private async Task<int> ServiceId(HttpClient http, string name) =>
        (await http.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Single(s => s.Name == name).Id;

    private static string AvailabilityUrl(string employeeId, DateOnly date, bool all, params int[] serviceIds) =>
        $"/api/v1/employees/{employeeId}/availability?date={date:yyyy-MM-dd}&allTimes={all.ToString().ToLowerInvariant()}"
        + string.Concat(serviceIds.Select(i => $"&serviceIds={i}"));

    // ----------------------------------------------------------------- services

    [Fact]
    public async Task Clients_can_read_but_not_manage_services()
    {
        var client = await Client();

        var list = await client.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services");
        list.Should().NotBeEmpty().And.OnlyContain(s => s.IsActive);

        var create = await client.PostAsJsonAsync("/api/v1/services", new ServiceUpsertRequest("Hak", null, 10, 30));
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"/api/v1/services/{list![0].Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_manages_the_service_lifecycle()
    {
        var admin = await Admin();
        var name = $"Usluga {Guid.NewGuid():N}"[..20];

        var created = await admin.PostAsJsonAsync("/api/v1/services", new ServiceUpsertRequest(name, "Opis", 20m, 45));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var service = (await created.Content.ReadFromJsonAsync<ServiceDto>())!;

        (await admin.PostAsJsonAsync("/api/v1/services", new ServiceUpsertRequest(name, null, 20m, 45)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var updated = await admin.PutAsJsonAsync($"/api/v1/services/{service.Id}", new ServiceUpsertRequest(name, "Novi opis", 25m, 60));
        (await updated.Content.ReadFromJsonAsync<ServiceDto>())!.Price.Should().Be(25m);

        (await admin.DeleteAsync($"/api/v1/services/{service.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var client = await Client();
        (await client.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services"))!.Should().NotContain(s => s.Id == service.Id);
        (await admin.GetFromJsonAsync<List<ServiceDto>>("/api/v1/services?includeInactive=true"))!
            .Should().Contain(s => s.Id == service.Id && !s.IsActive);
    }

    [Theory]
    [InlineData("", 10, 30)]
    [InlineData("Usluga", 0, 30)]
    [InlineData("Usluga", -5, 30)]
    [InlineData("Usluga", 10, 7)]    // not a multiple of 5
    [InlineData("Usluga", 10, 0)]
    [InlineData("Usluga", 10, 600)]
    public async Task Invalid_service_data_is_rejected(string name, decimal price, int minutes)
    {
        var res = await (await Admin()).PostAsJsonAsync("/api/v1/services", new ServiceUpsertRequest(name, null, price, minutes));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Services_can_be_filtered_to_what_an_employee_performs()
    {
        var client = await Client();
        var tarik = await Employee(client, "Tarik");

        var list = await client.GetFromJsonAsync<List<ServiceDto>>($"/api/v1/services?employeeId={tarik.Id}");

        list!.Select(s => s.Id).Should().BeEquivalentTo(tarik.ServiceIds);
        list.Should().HaveCount(3);
    }

    // ---------------------------------------------------------------- employees

    [Fact]
    public async Task Employee_list_hides_contact_details_from_clients_but_not_admins()
    {
        var clientView = (await (await Client()).GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!;
        var adminView = (await (await Admin()).GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!;

        clientView.Should().HaveCountGreaterThanOrEqualTo(2);
        clientView.Should().OnlyContain(e => e.Email == null && e.Phone == null);
        adminView.Should().Contain(e => e.Email == "kerim@ottoman.local");
    }

    [Fact]
    public async Task Admin_creates_an_employee_who_can_log_in_with_the_employee_role()
    {
        var admin = await Admin();
        var email = TestClient.UniqueEmail("emp");
        var serviceId = await ServiceId(admin, "Šišanje");

        var res = await admin.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "Novi", "Frizer", email, "061 111 222", "Emp_Pass123!", "Junior Barber", "Bio", [serviceId]));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await res.Content.ReadFromJsonAsync<EmployeeDto>())!;
        created.ServiceIds.Should().Equal(serviceId);

        var login = await api.CreateClient().LoginOk(email, "Emp_Pass123!");
        login.User.Roles.Should().BeEquivalentTo([Roles.Employee]);
        (await api.CreateClient().As(login).GetAsync("/api/v1/clients")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await admin.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "Isti", "Email", email, null, "Emp_Pass123!", "Barber", null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Only_admins_can_create_or_change_employees()
    {
        var client = await Client();
        var (_, kerim) = await Kerim();
        var request = new CreateEmployeeRequest("A", "B", TestClient.UniqueEmail(), null, "Emp_Pass123!", "Barber", null, null);

        (await client.PostAsJsonAsync("/api/v1/employees", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await kerim.PostAsJsonAsync("/api/v1/employees", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Weak_employee_password_is_rejected()
    {
        var res = await (await Admin()).PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "A", "B", TestClient.UniqueEmail(), null, "weak", "Barber", null, null));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Assigning_services_replaces_the_set_and_rejects_unknown_ids()
    {
        var admin = await Admin();
        var email = TestClient.UniqueEmail("svc");
        var created = (await (await admin.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "S", "Rvc", email, null, "Emp_Pass123!", "Barber", null, null))).Content.ReadFromJsonAsync<EmployeeDto>())!;
        var a = await ServiceId(admin, "Šišanje");
        var b = await ServiceId(admin, "Masaža glave");

        var set = await admin.PutAsJsonAsync($"/api/v1/employees/{created.Id}/services", new SetEmployeeServicesRequest([a, b]));
        (await set.Content.ReadFromJsonAsync<EmployeeDto>())!.ServiceIds.Should().BeEquivalentTo([a, b]);

        var shrink = await admin.PutAsJsonAsync($"/api/v1/employees/{created.Id}/services", new SetEmployeeServicesRequest([b]));
        (await shrink.Content.ReadFromJsonAsync<EmployeeDto>())!.ServiceIds.Should().Equal(b);

        (await admin.PutAsJsonAsync($"/api/v1/employees/{created.Id}/services", new SetEmployeeServicesRequest([999_999])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivated_employees_disappear_for_clients_and_cannot_be_booked()
    {
        var admin = await Admin();
        var email = TestClient.UniqueEmail("gone");
        var serviceId = await ServiceId(admin, "Šišanje");
        var created = (await (await admin.PostAsJsonAsync("/api/v1/employees", new CreateEmployeeRequest(
            "Odlazi", "Frizer", email, null, "Emp_Pass123!", "Barber", null, [serviceId]))).Content.ReadFromJsonAsync<EmployeeDto>())!;
        await admin.PutAsJsonAsync($"/api/v1/employees/{created.Id}/working-hours", new SetWorkingHoursRequest(
            [new(DayOfWeek.Monday, "09:00", "17:00", null, null)]));

        var deactivate = await admin.PutAsJsonAsync($"/api/v1/employees/{created.Id}", new UpdateEmployeeRequest(
            "Odlazi", "Frizer", null, "Barber", null, IsActive: false));
        deactivate.EnsureSuccessStatusCode();

        var client = await Client();
        (await client.GetFromJsonAsync<List<EmployeeDto>>("/api/v1/employees"))!.Should().NotContain(e => e.Id == created.Id);
        (await client.GetAsync(AvailabilityUrl(created.Id, Monday, false, serviceId))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------ working hours

    [Fact]
    public async Task Working_hours_are_visible_to_admin_and_the_employee_but_not_to_others()
    {
        var (kerimId, kerim) = await Kerim();
        var admin = await Admin();
        var tarikId = (await Employee(admin, "Tarik")).Id;

        var own = await kerim.GetFromJsonAsync<List<WorkingHoursDto>>($"/api/v1/employees/{kerimId}/working-hours");
        own!.Should().HaveCount(6).And.Contain(d => d.DayOfWeek == DayOfWeek.Monday && d.BreakStart == "12:00");

        (await kerim.GetAsync($"/api/v1/employees/{tarikId}/working-hours")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync($"/api/v1/employees/{tarikId}/working-hours")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await (await Client()).GetAsync($"/api/v1/employees/{kerimId}/working-hours")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("17:00", "09:00", null, null)]      // end before start
    [InlineData("09:00", "17:00", "12:00", null)]   // half a break
    [InlineData("09:00", "17:00", "08:00", "09:00")] // break outside hours
    [InlineData("09:03", "17:00", null, null)]      // off the 5-minute grid
    [InlineData("9am", "17:00", null, null)]        // bad format
    public async Task Invalid_working_hours_are_rejected(string start, string end, string? bs, string? be)
    {
        var admin = await Admin();
        var id = (await Employee(admin, "Tarik")).Id;

        var res = await admin.PutAsJsonAsync($"/api/v1/employees/{id}/working-hours",
            new SetWorkingHoursRequest([new(DayOfWeek.Wednesday, start, end, bs, be)]));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_days_in_working_hours_are_rejected()
    {
        var admin = await Admin();
        var id = (await Employee(admin, "Tarik")).Id;

        var res = await admin.PutAsJsonAsync($"/api/v1/employees/{id}/working-hours", new SetWorkingHoursRequest(
            [new(DayOfWeek.Monday, "09:00", "12:00", null, null), new(DayOfWeek.Monday, "13:00", "17:00", null, null)]));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------- availability

    [Fact]
    public async Task Default_offer_is_the_fifteen_minute_grid_and_show_all_adds_five_minute_starts()
    {
        var client = await Client();
        var kerim = await Employee(client, "Kerim");
        var sisanje = await ServiceId(client, "Šišanje"); // 30 min

        var offered = (await client.GetFromJsonAsync<AvailabilityDto>(AvailabilityUrl(kerim.Id, Monday, false, sisanje)))!;
        var all = (await client.GetFromJsonAsync<AvailabilityDto>(AvailabilityUrl(kerim.Id, Monday, true, sisanje)))!;

        offered.DurationMinutes.Should().Be(30);
        offered.TotalPrice.Should().Be(25m);
        offered.Starts.Should().Contain([Utc(Monday, 9), Utc(Monday, 9, 15), Utc(Monday, 16, 30)]);
        offered.Starts.Should().NotContain(Utc(Monday, 9, 5));
        offered.LocalTimes.Should().HaveCount(offered.Starts.Count);
        offered.LocalTimes.First().Should().Be("09:00");
        all.Starts.Should().Contain(Utc(Monday, 9, 5));
        all.Starts.Count.Should().BeGreaterThan(offered.Starts.Count);
    }

    [Fact]
    public async Task Lunch_break_is_never_offered()
    {
        var client = await Client();
        var kerim = await Employee(client, "Kerim");
        var sisanje = await ServiceId(client, "Šišanje");

        var all = (await client.GetFromJsonAsync<AvailabilityDto>(AvailabilityUrl(kerim.Id, Monday, true, sisanje)))!;

        all.Starts.Should().Contain(Utc(Monday, 11, 30));     // ends exactly at the break
        all.Starts.Should().NotContain(Utc(Monday, 11, 35));
        all.Starts.Should().NotContain(Utc(Monday, 12, 0));
        all.Starts.Should().Contain(Utc(Monday, 12, 30));     // right after the break
    }

    [Fact]
    public async Task Multiple_services_add_up_duration_and_price()
    {
        var client = await Client();
        var kerim = await Employee(client, "Kerim");
        var a = await ServiceId(client, "Šišanje");           // 30 min, 25
        var b = await ServiceId(client, "Uređivanje brade");  // 30 min, 15

        var res = (await client.GetFromJsonAsync<AvailabilityDto>(AvailabilityUrl(kerim.Id, Monday, false, a, b)))!;

        res.DurationMinutes.Should().Be(60);
        res.TotalPrice.Should().Be(40m);
        res.Starts.Should().NotContain(Utc(Monday, 16, 30)); // 60 minutes no longer fits at 16:30
        res.Starts.Should().Contain(Utc(Monday, 16, 0));
    }

    [Fact]
    public async Task A_day_off_has_no_availability()
    {
        var client = await Client();
        var kerim = await Employee(client, "Kerim");
        var sisanje = await ServiceId(client, "Šišanje");

        var res = (await client.GetFromJsonAsync<AvailabilityDto>(AvailabilityUrl(kerim.Id, Sunday, false, sisanje)))!;

        res.Starts.Should().BeEmpty();
    }

    [Fact]
    public async Task Employee_cannot_be_booked_for_a_service_they_do_not_perform()
    {
        var client = await Client();
        var tarik = await Employee(client, "Tarik");
        var notAssigned = await ServiceId(client, "Srednja / duga kosa"); // only Kerim

        var res = await client.GetAsync(AvailabilityUrl(tarik.Id, Monday, false, notAssigned));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Availability_requires_services_and_a_real_employee()
    {
        var client = await Client();
        var kerim = await Employee(client, "Kerim");
        var sisanje = await ServiceId(client, "Šišanje");

        (await client.GetAsync(AvailabilityUrl(kerim.Id, Monday, false))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync(AvailabilityUrl("does-not-exist", Monday, false, sisanje))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await api.CreateClient().GetAsync(AvailabilityUrl(kerim.Id, Monday, false, sisanje))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Existing_reservations_block_overlapping_starts_but_cancelled_ones_do_not()
    {
        var client = await Client();
        var kerim = await Employee(client, "Kerim");
        var sisanje = await ServiceId(client, "Šišanje");
        string clientId;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OttomanDbContext>();
            clientId = (await db.Users.FirstAsync(u => u.Email == "klijent@ottoman.local")).Id;
            db.Reservations.AddRange(
                new Reservation { ClientId = clientId, EmployeeId = kerim.Id, StartUtc = Utc(Tuesday, 9, 30), EndUtc = Utc(Tuesday, 10, 0), Status = ReservationStatus.Confirmed, CreatedAtUtc = DateTime.UtcNow },
                new Reservation { ClientId = clientId, EmployeeId = kerim.Id, StartUtc = Utc(Tuesday, 14, 0), EndUtc = Utc(Tuesday, 14, 30), Status = ReservationStatus.Cancelled, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var all = (await client.GetFromJsonAsync<AvailabilityDto>(AvailabilityUrl(kerim.Id, Tuesday, true, sisanje)))!;

        all.Starts.Should().Contain(Utc(Tuesday, 9, 0));           // ends exactly when the booking starts
        all.Starts.Should().NotContain(Utc(Tuesday, 9, 5));
        all.Starts.Should().NotContain(Utc(Tuesday, 9, 30));
        all.Starts.Should().Contain(Utc(Tuesday, 10, 0));
        all.Starts.Should().Contain(Utc(Tuesday, 14, 0));          // cancelled booking frees the slot
    }

    [Fact]
    public async Task Time_off_removes_slots_and_deleting_it_restores_them()
    {
        var admin = await Admin();
        var tarik = await Employee(admin, "Tarik");
        var sisanje = await ServiceId(admin, "Šišanje");
        var date = new DateOnly(2030, 1, 9); // Wednesday

        var add = await admin.PostAsJsonAsync($"/api/v1/employees/{tarik.Id}/time-off",
            new CreateTimeOffRequest(Utc(date, 9), Utc(date, 13), "Godišnji"));
        add.StatusCode.Should().Be(HttpStatusCode.OK);
        var timeOff = (await add.Content.ReadFromJsonAsync<TimeOffDto>())!;

        var blocked = (await admin.GetFromJsonAsync<AvailabilityDto>(AvailabilityUrl(tarik.Id, date, true, sisanje)))!;
        blocked.Starts.Should().OnlyContain(s => s >= Utc(date, 13));

        (await admin.DeleteAsync($"/api/v1/employees/{tarik.Id}/time-off/{timeOff.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var restored = (await admin.GetFromJsonAsync<AvailabilityDto>(AvailabilityUrl(tarik.Id, date, true, sisanje)))!;
        restored.Starts.Should().Contain(Utc(date, 9));
    }

    [Fact]
    public async Task One_employees_time_off_cannot_be_deleted_through_another_employee()
    {
        var admin = await Admin();
        var tarik = await Employee(admin, "Tarik");
        var kerim = await Employee(admin, "Kerim");
        var date = new DateOnly(2030, 1, 10);
        var timeOff = (await (await admin.PostAsJsonAsync($"/api/v1/employees/{tarik.Id}/time-off",
            new CreateTimeOffRequest(Utc(date, 9), Utc(date, 10), null))).Content.ReadFromJsonAsync<TimeOffDto>())!;

        (await admin.DeleteAsync($"/api/v1/employees/{kerim.Id}/time-off/{timeOff.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetFromJsonAsync<List<TimeOffDto>>($"/api/v1/employees/{tarik.Id}/time-off"))!.Should().Contain(t => t.Id == timeOff.Id);
    }

    [Fact]
    public async Task Time_off_must_end_after_it_starts()
    {
        var admin = await Admin();
        var tarik = await Employee(admin, "Tarik");

        var res = await admin.PostAsJsonAsync($"/api/v1/employees/{tarik.Id}/time-off",
            new CreateTimeOffRequest(Utc(Monday, 12), Utc(Monday, 10), null));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ----------------------------------------------------------------- settings

    [Fact]
    public async Task Anyone_signed_in_can_read_settings_but_only_admins_change_them()
    {
        var client = await Client();
        var settings = (await client.GetFromJsonAsync<SalonSettingsDto>("/api/v1/settings"))!;
        settings.SlotMinutes.Should().Be(5);
        settings.TimeZoneId.Should().Be("Europe/Sarajevo");

        var update = new UpdateSalonSettingsRequest(settings.Name, settings.Address, settings.Phone, settings.TimeZoneId,
            settings.Currency, settings.OfferGridMinutes, settings.MinLeadMinutes, settings.CancelWindowMinutes, settings.LowStockDefault);
        (await client.PutAsJsonAsync("/api/v1/settings", update)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await (await Admin()).PutAsJsonAsync("/api/v1/settings", update)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Not/AZone", "BAM", 15)]
    [InlineData("Europe/Sarajevo", "bam", 15)]
    [InlineData("Europe/Sarajevo", "BAM", 7)]
    public async Task Invalid_settings_are_rejected(string zone, string currency, int grid)
    {
        var res = await (await Admin()).PutAsJsonAsync("/api/v1/settings",
            new UpdateSalonSettingsRequest("Ottoman", null, null, zone, currency, grid, 30, 120, 5));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
