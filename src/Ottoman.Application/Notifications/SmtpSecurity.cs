namespace Ottoman.Application.Notifications;

/// <summary>How the connection to the SMTP server is protected.</summary>
public enum SmtpSecurityMode
{
    /// <summary>Plain SMTP, upgraded only if the server offers it. Only for a local inbox without a password.</summary>
    StartTlsWhenAvailable,

    /// <summary>STARTTLS is required: the connection fails rather than sending anything in clear.</summary>
    StartTls,

    /// <summary>TLS from the first byte (port 465).</summary>
    SslOnConnect,
}

public static class SmtpSecurity
{
    /// <summary>
    /// Picks the protection for the SMTP connection. With credentials it is never "when available": a server (or an
    /// attacker in between) that does not offer STARTTLS would otherwise receive the password in clear.
    /// <paramref name="configured"/> (Smtp:Security) overrides the choice, but cannot drop TLS while a password is set.
    /// </summary>
    public static SmtpSecurityMode Choose(int port, bool hasCredentials, string? configured = null)
    {
        if (Enum.TryParse<SmtpSecurityMode>(configured?.Trim(), ignoreCase: true, out var chosen))
            return hasCredentials && chosen == SmtpSecurityMode.StartTlsWhenAvailable ? DefaultSecure(port) : chosen;

        return hasCredentials ? DefaultSecure(port) : SmtpSecurityMode.StartTlsWhenAvailable;
    }

    private static SmtpSecurityMode DefaultSecure(int port) =>
        port == 465 ? SmtpSecurityMode.SslOnConnect : SmtpSecurityMode.StartTls;
}
