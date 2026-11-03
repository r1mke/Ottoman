using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Application.Common;
using Ottoman.Application.Reviews;
using Ottoman.Domain;

namespace Ottoman.Api.Controllers;

[ApiController]
[Route("api/v1/reviews")]
[Authorize]
public class ReviewsController(IReviewService reviews) : ControllerBase
{
    /// <summary>Reviews of one barber, service or product, with the average. Hidden ones are for admins only.</summary>
    [HttpGet]
    public Task<ReviewListDto> List(
        [FromQuery] ReviewTargetType targetType,
        [FromQuery] string targetKey,
        [FromQuery] bool includeHidden = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        reviews.ListAsync(User.ToActor(), targetType, targetKey, includeHidden, new PageQuery(page, pageSize), ct);

    /// <summary>Average rating and review count for every target of a kind (to show stars in lists).</summary>
    [HttpGet("summary")]
    public async Task<IReadOnlyList<ReviewSummaryDto>> Summary(
        [FromQuery] ReviewTargetType targetType, [FromQuery] int page = 1, [FromQuery] int pageSize = PageQuery.MaxPageSize, CancellationToken ct = default) =>
        this.Page(await reviews.SummaryPageAsync(targetType, new PageQuery(page, pageSize), ct));

    /// <summary>Can the signed-in client review this? Only after a completed appointment or a collected order.</summary>
    [HttpGet("eligibility")]
    [Authorize(Roles = Roles.Client)]
    public Task<EligibilityDto> Eligibility([FromQuery] ReviewTargetType targetType, [FromQuery] string targetKey, CancellationToken ct) =>
        reviews.EligibilityAsync(User.ToActor(), targetType, targetKey, ct);

    [HttpPost]
    [Authorize(Roles = Roles.Client)]
    public async Task<ActionResult<ReviewDto>> Create(CreateReviewRequest request, CancellationToken ct)
    {
        var result = await reviews.CreateAsync(User.ToActor(), request, ct);
        return result.Succeeded ? Created($"api/v1/reviews/{result.Value!.Id}", result.Value) : this.ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Client)]
    public async Task<ActionResult<ReviewDto>> Update(int id, UpdateReviewRequest request, CancellationToken ct) =>
        this.ToActionResult(await reviews.UpdateAsync(User.ToActor(), id, request, ct));

    /// <summary>The author removes their own review; an admin may remove any.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = $"{Roles.Client},{Roles.Admin}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        this.ToNoContent(await reviews.DeleteAsync(User.ToActor(), id, ct));

    // ---------------------------------------------------------------- moderation (admin)

    [HttpGet("all")]
    [Authorize(Policy = Policies.AdminOnly)]
    public Task<PagedResult<ReviewDto>> All(
        [FromQuery] bool onlyHidden = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        reviews.ListAllAsync(onlyHidden, new PageQuery(page, pageSize), ct);

    [HttpPut("{id:int}/hide")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ReviewDto>> Hide(int id, HideRequest request, CancellationToken ct) =>
        this.ToActionResult(await reviews.SetHiddenAsync(id, request.Hidden!.Value, ct));

    [HttpPost("{id:int}/reply")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<ActionResult<ReviewDto>> Reply(int id, ReplyRequest request, CancellationToken ct) =>
        this.ToActionResult(await reviews.ReplyAsync(id, request.Text, ct));
}
