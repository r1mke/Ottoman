using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Common;
using Ottoman.Application.Reservations;
using Ottoman.Domain;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/reservations")]
[Authorize]
public class ReservationsController(IReservationService reservations) : ControllerBase
{
    /// <summary>
    /// Books an appointment. The server re-derives availability and the price; the client only says
    /// who, what and when. Returns 409 when the slot is no longer free.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Client},{Roles.Admin}")]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationRequest request, CancellationToken ct)
    {
        var result = await reservations.CreateAsync(User.ToActor(), request, ct);
        return result.Succeeded
            ? CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value)
            : this.ToActionResult(result);
    }

    /// <summary>
    /// Clients: their own reservations. Employees: their schedule. Admins: all, with filters.
    /// <c>upcoming=true</c> = still to happen, <c>false</c> = history.
    /// </summary>
    [HttpGet]
    public Task<PagedResult<ReservationDto>> List(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? employeeId,
        [FromQuery] string? clientId,
        [FromQuery] ReservationStatus? status,
        [FromQuery] bool? upcoming,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        reservations.ListAsync(
            User.ToActor(),
            new ReservationFilter(fromUtc, toUtc, employeeId, clientId, status, upcoming),
            new PageQuery(page, pageSize), ct);

    [HttpGet("summary")]
    [Authorize(Policy = Policies.Staff)]
    public Task<ReservationSummaryDto> Summary(
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] string? employeeId, CancellationToken ct) =>
        reservations.SummaryAsync(User.ToActor(), fromUtc, toUtc, employeeId, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationDto>> Get(int id, CancellationToken ct) =>
        this.ToActionResult(await reservations.GetAsync(User.ToActor(), id, ct));

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ReservationDto>> Cancel(int id, CancelReservationRequest? request, CancellationToken ct) =>
        this.ToActionResult(await reservations.CancelAsync(User.ToActor(), id, request?.Reason, ct));

    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<ReservationDto>> Complete(int id, CancellationToken ct) =>
        this.ToActionResult(await reservations.CompleteAsync(User.ToActor(), id, ct));

    [HttpPost("{id:int}/no-show")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<ReservationDto>> NoShow(int id, CancellationToken ct) =>
        this.ToActionResult(await reservations.MarkNoShowAsync(User.ToActor(), id, ct));

    [HttpPut("{id:int}/reschedule")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ReservationDto>> Reschedule(int id, RescheduleReservationRequest request, CancellationToken ct) =>
        this.ToActionResult(await reservations.RescheduleAsync(User.ToActor(), id, request, ct));
}
