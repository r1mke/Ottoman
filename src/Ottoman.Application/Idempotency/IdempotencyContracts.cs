using System.Security.Cryptography;
using System.Text;

namespace Ottoman.Application.Idempotency;

public enum BeginOutcome
{
    /// <summary>First time this key is seen: run the action.</summary>
    Proceed,

    /// <summary>Same request already finished: answer with the stored response, do nothing.</summary>
    Replay,

    /// <summary>Same request is still running (a double tap): refuse, the first one will finish.</summary>
    InProgress,

    /// <summary>The key was used for a different request: a client bug, never silently reuse an answer.</summary>
    Mismatch,
}

public record BeginResult(BeginOutcome Outcome, int StatusCode = 0, string? Body = null, string? ContentType = null);

/// <summary>
/// Remembers the outcome of a write so that sending it twice (double tap, retry after a lost response) has the
/// effect of sending it once. Only successful responses are kept: a failed attempt leaves the key free to retry.
/// </summary>
public interface IIdempotencyStore
{
    Task<BeginResult> BeginAsync(string userId, string key, string route, string requestHash, CancellationToken ct);

    Task CompleteAsync(string userId, string key, int statusCode, string body, string contentType, CancellationToken ct);

    Task AbandonAsync(string userId, string key, CancellationToken ct);
}

public static class IdempotencyRules
{
    public const string Header = "Idempotency-Key";
    public const int MaxKeyLength = 64;

    /// <summary>A placeholder older than this belongs to a request that died; the key may be reused.</summary>
    public static readonly TimeSpan InProgressTimeout = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public static bool IsValidKey(string? key) =>
        !string.IsNullOrEmpty(key) && key.Length <= MaxKeyLength && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or ':' or '.');

    /// <summary>Stable fingerprint of what the request asked for, so the same key with other content is detected.</summary>
    public static string Hash(string canonicalRequest) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
}
