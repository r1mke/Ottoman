namespace Ottoman.Domain.Entities;

/// <summary>A client's rating of a barber, a service or a product they actually used.</summary>
public class Review
{
    public int Id { get; set; }
    public string ClientId { get; set; } = null!;
    public ReviewTargetType TargetType { get; set; }

    /// <summary>The employee's user id, or the service/product id as text.</summary>
    public string TargetKey { get; set; } = null!;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Hidden by the salon (moderation). Hidden reviews count for nothing and are shown to nobody but admins.</summary>
    public bool IsHidden { get; set; }
    public string? ReplyText { get; set; }
    public DateTime? ReplyAtUtc { get; set; }
}
