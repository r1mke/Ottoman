using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Ottoman.Application.Notifications;

namespace Ottoman.Worker.Email;

public class SmtpOptions
{
    public const string Section = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "noreply@ottoman.local";
    public string FromName { get; set; } = "Ottoman";

    /// <summary>Optional: StartTls, SslOnConnect or StartTlsWhenAvailable. Empty = chosen from port and credentials.</summary>
    public string? Security { get; set; }
}

public class MailKitEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody, string textBody, CancellationToken ct)
    {
        var o = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(o.FromName, o.From));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        var body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody };
        if (htmlBody.Contains($"cid:{EmailTemplate.LogoContentId}"))
        {
            using var logo = typeof(MailKitEmailSender).Assembly.GetManifestResourceStream("Ottoman.Worker.Email.logo.png");
            if (logo is not null)
            {
                var image = body.LinkedResources.Add("logo.png", logo);
                image.ContentId = EmailTemplate.LogoContentId;
            }
        }
        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        // Local Mailpit speaks plain SMTP; as soon as there is a password, TLS is required (never sent in clear).
        var hasCredentials = !string.IsNullOrEmpty(o.User);
        var security = SmtpSecurity.Choose(o.Port, hasCredentials, o.Security) switch
        {
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.StartTlsWhenAvailable,
        };
        await client.ConnectAsync(o.Host, o.Port, security, ct);
        if (hasCredentials) await client.AuthenticateAsync(o.User, o.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
