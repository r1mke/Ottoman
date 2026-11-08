using System.Net;
using FluentValidation;
using Ottoman.Application.Common;
using Ottoman.Domain;

namespace Ottoman.Application.Notifications;

/// <param name="Category">For a message that came from an announcement: its category (the apps show it as a label).</param>
public record NotificationDto(
    long Id, string Title, string Body, string Kind, DateTime CreatedAtUtc, bool IsRead, string? ImageUrl = null,
    AnnouncementCategory? Category = null);

public record UnreadCountDto(int Count);

public record AnnouncementRequest(
    string Title,
    string Body,
    AnnouncementAudience Audience,
    string? TargetUserId = null,
    bool SendEmail = false,
    AnnouncementCategory Category = AnnouncementCategory.News,
    DateTime? ExpiresAtUtc = null);

/// <summary>What the admin may change after sending: the text, the category and the end date. Never re-sends anything.</summary>
public record UpdateAnnouncementRequest(string Title, string Body, AnnouncementCategory Category, DateTime? ExpiresAtUtc);

public record AnnouncementHideRequest(bool? Hidden);

public class AnnouncementHideRequestValidator : AbstractValidator<AnnouncementHideRequest>
{
    public AnnouncementHideRequestValidator() =>
        RuleFor(x => x.Hidden).NotNull().WithMessage("Navedite da li se obavijest skriva ili prikazuje.");
}

public class UpdateAnnouncementRequestValidator : AbstractValidator<UpdateAnnouncementRequest>
{
    public UpdateAnnouncementRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120).WithName("Naslov");
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000).WithName("Poruka");
        RuleFor(x => x.Category).IsInEnum().WithName("Kategorija");
    }
}

/// <summary>Whether recipients still see an announcement. Pure, so the inbox query and the admin list agree.</summary>
public static class AnnouncementVisibility
{
    public const string ExpiryInPast = "Datum isteka mora biti u budućnosti.";

    public static bool Visible(bool isHidden, DateTime? expiresAtUtc, DateTime nowUtc) =>
        !isHidden && (expiresAtUtc is null || expiresAtUtc > nowUtc);

    public static bool Expired(DateTime? expiresAtUtc, DateTime nowUtc) => expiresAtUtc is { } e && e <= nowUtc;
}

public class AnnouncementRequestValidator : AbstractValidator<AnnouncementRequest>
{
    public AnnouncementRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120).WithName("Naslov");
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000).WithName("Poruka");
        RuleFor(x => x.Audience).IsInEnum();
        RuleFor(x => x.Category).IsInEnum().WithName("Kategorija");
        RuleFor(x => x.TargetUserId).NotEmpty()
            .When(x => x.Audience == AnnouncementAudience.User)
            .WithMessage("Odaberite korisnika kojem šaljete poruku.");
        RuleFor(x => x.TargetUserId).Empty()
            .When(x => x.Audience != AnnouncementAudience.User)
            .WithMessage("Pojedinačni primalac se navodi samo za publiku „Korisnik“.");
    }
}

public record AnnouncementDto(
    int Id,
    string Title,
    string Body,
    AnnouncementAudience Audience,
    bool SendEmail,
    int RecipientCount,
    DateTime CreatedAtUtc,
    string? ImageUrl = null,
    AnnouncementCategory Category = AnnouncementCategory.News,
    DateTime? ExpiresAtUtc = null,
    bool IsHidden = false,
    bool IsExpired = false,
    DateTime? UpdatedAtUtc = null);

/// <summary>The signed-in user's inbox plus the admin's announcements.</summary>
public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(string userId, bool unreadOnly, PageQuery page, CancellationToken ct);
    Task<int> UnreadCountAsync(string userId, CancellationToken ct);
    Task<Result<bool>> MarkReadAsync(string userId, long id, CancellationToken ct);
    Task<int> MarkAllReadAsync(string userId, CancellationToken ct);

    /// <param name="image">The picture of the news item, if the admin chose one.</param>
    Task<Result<AnnouncementDto>> CreateAnnouncementAsync(string adminId, AnnouncementRequest request, Stream? image, CancellationToken ct);
    /// <param name="search">Matches the title or the text.</param>
    Task<PagedResult<AnnouncementDto>> ListAnnouncementsAsync(string? search, PageQuery page, CancellationToken ct);
    Task<Result<AnnouncementDto>> GetAnnouncementAsync(int id, CancellationToken ct);

    /// <summary>Changes text, category and end date. Recipients see the new text in their inbox; nothing is sent again.</summary>
    Task<Result<AnnouncementDto>> UpdateAnnouncementAsync(int id, UpdateAnnouncementRequest request, CancellationToken ct);

    /// <summary>Hides it from (or shows it again to) every recipient. A status change, the row stays.</summary>
    Task<Result<AnnouncementDto>> SetAnnouncementHiddenAsync(int id, bool hidden, CancellationToken ct);

    /// <summary>Replaces (stream) or removes (null) the picture.</summary>
    Task<Result<AnnouncementDto>> SetAnnouncementImageAsync(int id, Stream? image, CancellationToken ct);
}

/// <summary>Sends one e-mail. Implemented with SMTP in the worker; faked in tests.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string textBody, CancellationToken ct);
}

/// <summary>The single place user-controlled text is escaped for HTML (names, notes, tokens, product names).</summary>
public static class EmailTemplate
{
    /// <summary>The Content-Id of the logo a sender attaches to every message built by <see cref="Html"/>.</summary>
    public const string LogoContentId = "ottoman-logo";

    public static string Html(string title, string body)
    {
        var paragraphs = body.Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => $"<p style=\"margin:0 0 14px;line-height:1.5\">{WebUtility.HtmlEncode(p).Replace("\n", "<br>")}</p>");
        return "<!doctype html><html><body style=\"margin:0;background:#f4f4f5;font-family:Arial,Helvetica,sans-serif;color:#18181b\">" +
               "<div style=\"max-width:560px;margin:24px auto;background:#fff;border-radius:12px;padding:28px\">" +
               // The logo is attached to the mail itself (see the sender); the alt text is what shows when images are off.
               $"<img src=\"cid:{LogoContentId}\" alt=\"OTTOMAN\" width=\"168\" style=\"display:block;margin-bottom:18px;font-weight:800;letter-spacing:3px\">" +
               $"<h2 style=\"margin:0 0 16px;font-size:20px\">{WebUtility.HtmlEncode(title)}</h2>" +
               string.Concat(paragraphs) +
               "</div></body></html>";
    }
}
