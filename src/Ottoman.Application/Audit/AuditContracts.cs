using Ottoman.Application.Common;

namespace Ottoman.Application.Audit;

/// <summary>What one staff action looked like from the outside. Never contains request bodies or secrets.</summary>
/// <remarks>Role <see cref="SystemRole"/> marks an action of the server itself; its UserId is not an account.</remarks>
public record AuditRecord(
    string UserId,
    string Role,
    string Method,
    string Action,
    string Route,
    string Target,
    int StatusCode,
    string? Ip);

public record AuditEntryDto(
    long Id,
    DateTime AtUtc,
    string UserId,
    string UserName,
    string Role,
    string Method,
    string Action,
    string Route,
    string Target,
    int StatusCode,
    string? Ip,
    /// <summary>What the target is called in words (a product's name, a client, an order's date); null when unknown.</summary>
    string? TargetLabel = null);

public record AuditQuery(string? Search, string? UserId, DateTime? FromUtc, DateTime? ToUtc);

public interface IAuditLog
{
    public const string SystemRole = "System";

    /// <summary>
    /// Writes the entry on its own connection. The action it describes has already happened, so a failure here
    /// is logged loudly and swallowed: failing the caller's request would not undo anything.
    /// </summary>
    Task RecordAsync(AuditRecord record, CancellationToken ct);
}

public interface IAuditQueries
{
    Task<PagedResult<AuditEntryDto>> ListAsync(AuditQuery query, PageQuery page, CancellationToken ct);
}

/// <summary>Reads the parts of an audit entry's action and target. Pure.</summary>
public static class AuditTargets
{
    /// <summary>Shown when the record the entry points at no longer exists.</summary>
    public const string Removed = "(obrisano)";

    /// <summary>"Products.Create" -> "Products".</summary>
    public static string Controller(string action)
    {
        var dot = action.IndexOf('.');
        return dot < 0 ? action : action[..dot];
    }

    /// <summary>The record the request was about: the <c>id</c> of "id=7, timeOffId=3" -> "7"; null when there is none.</summary>
    public static string? PrimaryId(string target)
    {
        foreach (var part in target.Split(','))
        {
            var pair = part.Trim();
            if (pair.StartsWith("id=", StringComparison.Ordinal) && pair.Length > 3) return pair[3..];
        }
        return null;
    }
}

/// <summary>Decides which requests are worth a line in the audit log. Pure.</summary>
public static class AuditPolicy
{
    /// <summary>Controllers whose writes are noise (read marks, push tokens) or are not staff actions (sign-in).</summary>
    private static readonly string[] Ignored = ["Notifications", "Devices", "Auth", "Me", "Payments"];

    public static bool ShouldRecord(string httpMethod, string controller, int statusCode, bool isStaff)
    {
        if (!isStaff || statusCode >= 400) return false;
        if (httpMethod is "GET" or "HEAD" or "OPTIONS") return false;
        return !Ignored.Contains(controller, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>How long entries are kept. A year covers a tax period and any dispute that is still alive.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(365);
}
