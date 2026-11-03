namespace Ottoman.Domain.Entities;

/// <summary>A message in one user's in-app inbox (the bell).</summary>
public class UserNotification
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;

    /// <summary>Machine-readable category, e.g. "ReservationCreated", "Announcement". Lets the apps pick an icon.</summary>
    public string Kind { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public int? AnnouncementId { get; set; }
}

/// <summary>A message the salon composes for a group of users. History for the admin; recipients get <see cref="UserNotification"/> rows.</summary>
public class Announcement
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public AnnouncementAudience Audience { get; set; }
    public string? TargetUserId { get; set; }
    public bool SendEmail { get; set; }
    public int RecipientCount { get; set; }

    /// <summary>Storage key of the picture of the news item (optional).</summary>
    public string? ImageKey { get; set; }

    public string CreatedByUserId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Marks that a reminder was sent. UNIQUE(ReservationId, Kind) makes duplicates impossible.</summary>
public class ReservationReminder
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public ReminderKind Kind { get; set; }
    public DateTime SentAtUtc { get; set; }
}

/// <summary>Outbox events already handled by the notification consumer (RabbitMQ delivers at least once).</summary>
public class ProcessedEvent
{
    public long OutboxMessageId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
}

/// <summary>
/// One e-mail of an event that has gone out. An event that fails half-way through its mails is retried as a whole;
/// these rows let the retry skip the people who already got theirs.
/// </summary>
public class SentEmail
{
    public long OutboxMessageId { get; set; }

    /// <summary>Hash of recipient address, subject and text: the same person can get two different mails of one event.</summary>
    public string RecipientKey { get; set; } = null!;
    public DateTime SentAtUtc { get; set; }
}

/// <summary>A phone that can receive push messages for a user. The token is unique: a phone belongs to whoever signed in last.</summary>
public class DeviceToken
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Token { get; set; } = null!;
    public string Platform { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

/// <summary>One line of the staff audit trail. No foreign key to the user: history must outlive the account.</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public DateTime AtUtc { get; set; }
    public string UserId { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Method { get; set; } = null!;
    public string Action { get; set; } = null!;
    public string Route { get; set; } = null!;
    public string Target { get; set; } = null!;
    public int StatusCode { get; set; }
    public string? Ip { get; set; }
}

/// <summary>The remembered outcome of a write, keyed by the caller's Idempotency-Key. StatusCode stays null while it runs.</summary>
public class IdempotencyRecord
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Route { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? ContentType { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
