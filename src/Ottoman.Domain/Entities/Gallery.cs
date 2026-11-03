namespace Ottoman.Domain.Entities;

/// <summary>A haircut style the gallery is filtered by (buzz cut, french crop, long hair, ...). Managed by the admin.</summary>
public class HaircutCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
}

/// <summary>One finished haircut: the picture, its style and the barber who did it (kept for later per-barber sections).</summary>
public class GalleryItem
{
    public int Id { get; set; }
    public string ImageKey { get; set; } = null!;

    /// <summary>SHA-256 of the uploaded bytes (hex): the same picture cannot be added twice.</summary>
    public string? ContentHash { get; set; }
    public int CategoryId { get; set; }

    /// <summary>The barber who did it; null when that barber is gone or was never recorded.</summary>
    public string? EmployeeId { get; set; }
    public string? Caption { get; set; }

    /// <summary>Shown in "Our work" on the home screen.</summary>
    public bool IsFeatured { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>The salon's opening time for one weekday; both times null means closed.</summary>
public class SalonOpeningHours
{
    public DayOfWeek Day { get; set; }

    /// <summary>Minutes after midnight (local salon time).</summary>
    public int? OpenMinutes { get; set; }
    public int? CloseMinutes { get; set; }
}
