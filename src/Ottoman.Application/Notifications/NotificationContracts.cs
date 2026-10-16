using System.Net;
using FluentValidation;
using Ottoman.Application.Common;
using Ottoman.Domain;

namespace Ottoman.Application.Notifications;

public record NotificationDto(long Id, string Title, string Body, string Kind, DateTime CreatedAtUtc, bool IsRead, string? ImageUrl = null);

public record UnreadCountDto(int Count);

public record AnnouncementRequest(
    string Title,
    string Body,
    AnnouncementAudience Audience,
    string? TargetUserId = null,
    bool SendEmail = false);

public class AnnouncementRequestValidator : AbstractValidator<AnnouncementRequest>
{
    public AnnouncementRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120).WithName("Naslov");
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000).WithName("Poruka");
        RuleFor(x => x.Audience).IsInEnum();
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
    string? ImageUrl = null);

/// <summary>The signed-in user's inbox plus the admin's announcements.</summary>
public interface INotificationService
{
    Task<PagedResult<NotificationDto>> ListAsync(string userId, bool unreadOnly, PageQuery page, CancellationToken ct);
    Task<int> UnreadCountAsync(string userId, CancellationToken ct);
    Task<Result<bool>> MarkReadAsync(string userId, long id, CancellationToken ct);
    Task<int> MarkAllReadAsync(string userId, CancellationToken ct);

    Task<Result<AnnouncementDto>> CreateAnnouncementAsync(string adminId, AnnouncementRequest request, CancellationToken ct);
    Task<PagedResult<AnnouncementDto>> ListAnnouncementsAsync(PageQuery page, CancellationToken ct);
    Task<Result<AnnouncementDto>> GetAnnouncementAsync(int id, CancellationToken ct);
}

/// <summary>Sends one e-mail. Implemented with SMTP in the worker; faked in tests.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string textBody, CancellationToken ct);
}

/// <summary>The single place user-controlled text is escaped for HTML (names, notes, tokens, product names).</summary>
public static class EmailTemplate
{
    public static string Html(string title, string body)
    {
        var paragraphs = body.Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => $"<p style=\"margin:0 0 14px;line-height:1.5\">{WebUtility.HtmlEncode(p).Replace("\n", "<br>")}</p>");
        return "<!doctype html><html><body style=\"margin:0;background:#f4f4f5;font-family:Arial,Helvetica,sans-serif;color:#18181b\">" +
               "<div style=\"max-width:560px;margin:24px auto;background:#fff;border-radius:12px;padding:28px\">" +
               "<div style=\"font-weight:800;letter-spacing:3px;margin-bottom:18px\">OTTOMAN</div>" +
               $"<h2 style=\"margin:0 0 16px;font-size:20px\">{WebUtility.HtmlEncode(title)}</h2>" +
               string.Concat(paragraphs) +
               "</div></body></html>";
    }
}
