namespace Ottoman.Domain.Entities;

/// <summary>A client's rating of a barber, a service or a product they actually used.</summary>
public class Review
{
    public int Id { get; set; }
    public string ClientId { get; set; } = null!;
    public ReviewTargetType TargetType { get; set; }


    // Exactly one of these is set, the one that matches TargetType (the database checks it). Real foreign keys, so a
    // review cannot point at something that does not exist.
    public int? ProductId { get; set; }
    public int? ServiceId { get; set; }

    /// <summary>The barber's profile (its key is the barber's user id).</summary>
    public string? EmployeeProfileId { get; set; }

    /// <summary>
    /// The target as the apps address it: the barber's user id, or the service/product id as text. Not stored: it is
    /// read from the typed key that matches <see cref="TargetType"/>.
    /// </summary>
    public string TargetKey => KeyOf(TargetType, ProductId, ServiceId, EmployeeProfileId);

    public static string KeyOf(ReviewTargetType type, int? productId, int? serviceId, string? employeeProfileId) => type switch
    {
        ReviewTargetType.Product => productId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
        ReviewTargetType.Service => serviceId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
        _ => employeeProfileId ?? "",
    };

    /// <summary>Points the review at a target given as the apps send it; the key must already be in canonical form.</summary>
    public void SetTarget(ReviewTargetType type, string key)
    {
        TargetType = type;
        ProductId = type == ReviewTargetType.Product ? int.Parse(key, System.Globalization.CultureInfo.InvariantCulture) : null;
        ServiceId = type == ReviewTargetType.Service ? int.Parse(key, System.Globalization.CultureInfo.InvariantCulture) : null;
        EmployeeProfileId = type == ReviewTargetType.Employee ? key : null;
    }

    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Hidden by the salon (moderation). Hidden reviews count for nothing and are shown to nobody but admins.</summary>
    public bool IsHidden { get; set; }
    public string? ReplyText { get; set; }
    public DateTime? ReplyAtUtc { get; set; }
}
