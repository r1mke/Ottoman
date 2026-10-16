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
