using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ottoman.Api.Auth;
using Ottoman.Api.Filters;
using Ottoman.Application.Common;
using Ottoman.Application.Notifications;

namespace Ottoman.Api.Controllers;

/// <summary>The signed-in user's inbox. Every query is scoped to the caller's id from the token.</summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<NotificationDto>> List(
        [FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        notifications.ListAsync(User.UserId(), unreadOnly, new PageQuery(page, pageSize), ct);

    [HttpGet("unread-count")]
    public async Task<UnreadCountDto> UnreadCount(CancellationToken ct) =>
        new(await notifications.UnreadCountAsync(User.UserId(), ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct) =>
        this.ToNoContent(await notifications.MarkReadAsync(User.UserId(), id, ct));

    [HttpPost("read-all")]
    public async Task<UnreadCountDto> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(User.UserId(), ct);
        return new UnreadCountDto(0);
    }
}

/// <summary>Messages the salon sends to a group of users (desktop "Notifikacije").</summary>
[ApiController]
[Route("api/v1/announcements")]
[Authorize(Policy = Policies.AdminOnly)]
public class AnnouncementsController(INotificationService notifications) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AnnouncementDto>> Create(AnnouncementRequest request, CancellationToken ct)
    {
        var result = await notifications.CreateAnnouncementAsync(User.UserId(), request, null, ct);
        return result.Succeeded ? Created($"api/v1/announcements/{result.Value!.Id}", result.Value) : this.ToActionResult(result);
    }

    /// <summary>The same, with a picture (JPEG, PNG or WebP, up to 5 MB) sent as a form.</summary>
    [HttpPost("with-image")]
    [RejectOversizedUpload]
    [RequestSizeLimit(ImageUpload.RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUpload.FormLimit)]
    public async Task<ActionResult<AnnouncementDto>> CreateWithImage([FromForm] AnnouncementRequest request, IFormFile? file, CancellationToken ct)
    {
        await using var stream = file?.OpenReadStream();
        var result = await notifications.CreateAnnouncementAsync(User.UserId(), request, stream, ct);
        return result.Succeeded ? Created($"api/v1/announcements/{result.Value!.Id}", result.Value) : this.ToActionResult(result);
    }

    [HttpGet]
    public Task<PagedResult<AnnouncementDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        notifications.ListAnnouncementsAsync(new PageQuery(page, pageSize), ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AnnouncementDto>> Get(int id, CancellationToken ct) =>
        this.ToActionResult(await notifications.GetAnnouncementAsync(id, ct));
}
