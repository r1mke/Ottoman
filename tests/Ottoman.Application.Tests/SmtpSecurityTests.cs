using FluentAssertions;
using Ottoman.Application.Notifications;

namespace Ottoman.Application.Tests;

public class SmtpSecurityTests
{
    [Fact]
    public void A_local_inbox_without_a_password_may_speak_plain_smtp() =>
        SmtpSecurity.Choose(1025, hasCredentials: false).Should().Be(SmtpSecurityMode.StartTlsWhenAvailable);

    [Theory]
    [InlineData(587, SmtpSecurityMode.StartTls)]
    [InlineData(25, SmtpSecurityMode.StartTls)]
    [InlineData(465, SmtpSecurityMode.SslOnConnect)]
    public void With_a_password_tls_is_required(int port, SmtpSecurityMode expected) =>
        SmtpSecurity.Choose(port, hasCredentials: true).Should().Be(expected);

    [Fact]
    public void The_setting_wins_but_cannot_drop_tls_while_a_password_is_set()
    {
        SmtpSecurity.Choose(2525, hasCredentials: true, "SslOnConnect").Should().Be(SmtpSecurityMode.SslOnConnect);
        SmtpSecurity.Choose(587, hasCredentials: true, "StartTlsWhenAvailable").Should().Be(SmtpSecurityMode.StartTls);
        SmtpSecurity.Choose(1025, hasCredentials: false, "starttls").Should().Be(SmtpSecurityMode.StartTls);
        SmtpSecurity.Choose(587, hasCredentials: true, "nonsense").Should().Be(SmtpSecurityMode.StartTls);
    }
}
