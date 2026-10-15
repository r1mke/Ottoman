using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Employees;
using Ottoman.Application.Scheduling;
using Ottoman.Domain;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/employees")]
[Authorize]
public class EmployeesController(IEmployeeService employees, IAvailabilityService availability) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(Roles.Admin);
    private bool IsAdminOrSelf(string id) => IsAdmin || User.UserId() == id;

    /// <summary>Active employees for clients; admins may include inactive ones and see contact details.</summary>
    [HttpGet]
    public Task<IReadOnlyList<EmployeeDto>> List([FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        employees.ListAsync(includeInactive && IsAdmin, IsAdmin, ct);

    [HttpGet("{id}")]
    public async Task<ActionResult<EmployeeDto>> Get(string id, CancellationToken ct) =>
        this.ToActionResult(await employees.GetAsync(id, IsAdmin, ct));

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken ct)
    {
        var result = await employees.CreateAsync(request, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : this.ToActionResult(result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> Update(string id, UpdateEmployeeRequest request, CancellationToken ct) =>
        this.ToActionResult(await employees.UpdateAsync(id, request, ct));

    [HttpPut("{id}/services")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> SetServices(string id, SetEmployeeServicesRequest request, CancellationToken ct) =>
        this.ToActionResult(await employees.SetServicesAsync(id, request.ServiceIds, ct));

    // ----- schedule (admin, or the employee for their own)

    [HttpGet("{id}/working-hours")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<IReadOnlyList<WorkingHoursDto>>> GetWorkingHours(string id, CancellationToken ct) =>
        IsAdminOrSelf(id) ? this.ToActionResult(await employees.GetWorkingHoursAsync(id, ct)) : Forbid();

    [HttpPut("{id}/working-hours")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<WorkingHoursDto>>> SetWorkingHours(
        string id, SetWorkingHoursRequest request, CancellationToken ct) =>
        this.ToActionResult(await employees.SetWorkingHoursAsync(id, request, ct));

    [HttpGet("{id}/time-off")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<IReadOnlyList<TimeOffDto>>> ListTimeOff(string id, CancellationToken ct) =>
        IsAdminOrSelf(id) ? this.ToActionResult(await employees.ListTimeOffAsync(id, ct)) : Forbid();

    [HttpPost("{id}/time-off")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<TimeOffDto>> AddTimeOff(string id, CreateTimeOffRequest request, CancellationToken ct) =>
        this.ToActionResult(await employees.AddTimeOffAsync(id, request, ct));

    [HttpDelete("{id}/time-off/{timeOffId:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> RemoveTimeOff(string id, int timeOffId, CancellationToken ct) =>
        this.ToNoContent(await employees.RemoveTimeOffAsync(id, timeOffId, ct));

    // ----- availability

    /// <summary>
    /// Start times for the chosen services. Default is the curated offer; <c>allTimes=true</c> lists every 5-minute start.
    /// </summary>
    [HttpGet("{id}/availability")]
    public async Task<ActionResult<AvailabilityDto>> Availability(
        string id,
        [FromQuery] DateOnly date,
        [FromQuery] int[] serviceIds,
        [FromQuery] bool allTimes = false,
        CancellationToken ct = default) =>
        this.ToActionResult(await availability.GetAsync(id, date, serviceIds, allTimes, ct));
}
