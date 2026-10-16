using FluentValidation;
using Ottoman.Application.Common;

namespace Ottoman.Application.Push;

public class PushOptions
{
    public const string Section = "Firebase";

    /// <summary>The service account key as JSON (from the environment). Empty = push is off.</summary>
    public string ServiceAccountJson { get; set; } = "";

    /// <summary>Alternative to the JSON: a path to the key file.</summary>
    public string ServiceAccountPath { get; set; } = "";

    public bool Configured => ServiceAccountJson != "" || ServiceAccountPath != "";

    /// <summary>Ask Firebase to check the message without delivering it (smoke tests against the real project).</summary>
    public bool ValidateOnly { get; set; }

    /// <summary>Most phones one account keeps; the oldest are dropped.</summary>
    public int MaxDevicesPerUser { get; set; } = 10;
}

public record PushMessage(string Title, string Body, string Kind);

public enum PushOutcome
{
    Sent,

    /// <summary>The token is no longer valid (app uninstalled, token rotated): forget it.</summary>
    InvalidToken,

    /// <summary>A transient or configuration problem: keep the token, try next time.</summary>
    Failed,
}

/// <summary>The push provider behind a port, so the logic is testable without Google.</summary>
public interface IPushSender
{
    Task<PushOutcome> SendAsync(string token, PushMessage message, CancellationToken ct);
}

public record RegisterDeviceRequest(string Token, string Platform);

public class RegisterDeviceRequestValidator : AbstractValidator<RegisterDeviceRequest>
{
    public RegisterDeviceRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512).Matches(@"^[A-Za-z0-9_\-:.]+$").WithMessage("Token nije ispravan.");
        RuleFor(x => x.Platform).Must(p => p is "android" or "ios").WithMessage("Platforma mora biti android ili ios.");
    }
}

public interface IDeviceRegistry
{
    /// <summary>Remembers the phone for the user. A token seen before moves to this user.</summary>
    Task RegisterAsync(string userId, RegisterDeviceRequest request, CancellationToken ct);

    /// <summary>Forgets the phone, but only if it belongs to the caller.</summary>
    Task UnregisterAsync(string userId, string token, CancellationToken ct);
}

/// <summary>Which messages become a push, and how they look on the lock screen. Pure.</summary>
public static class PushPolicy
{
    private static readonly string[] MailOnly = ["PasswordReset", "RegistrationAttempt"];

    /// <summary>Account-security messages carry codes and never go to a lock screen.</summary>
    public static bool ShouldPush(string kind) => !MailOnly.Contains(kind);

    /// <summary>One line is enough on a lock screen; the full text is in the inbox.</summary>
    public static string Preview(string body, int max = 160)
    {
        var oneLine = string.Join(' ', body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return oneLine.Length <= max ? oneLine : oneLine[..(max - 1)].TrimEnd() + "…";
    }
}
