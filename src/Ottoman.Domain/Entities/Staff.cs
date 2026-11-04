namespace Ottoman.Domain.Entities;

/// <summary>Employee-specific data. Shares its key with the Identity user (1:1).</summary>
public class EmployeeProfile
{
    public string UserId { get; set; } = null!;

    /// <summary>The barber's title from the shared list (a real foreign key: a title in use cannot be deleted).</summary>
    public int TitleId { get; set; }
    public EmployeeTitle Title { get; set; } = null!;
    public string? Bio { get; set; }
    public string? PhotoKey { get; set; }

    /// <summary>How close the avatar crops around the face: 1 = Cloudinary's default, 2 = twice as close.</summary>
    public double PhotoZoom { get; set; } = 1.0;

    /// <summary>Picks the default avatar while there is no photo. Null = not said.</summary>
    public Gender? Gender { get; set; }
    public bool IsActive { get; set; } = true;

    public List<EmployeeService> Services { get; set; } = [];
    public List<WorkingHours> WorkingHours { get; set; } = [];
    public List<TimeOff> TimeOff { get; set; } = [];
}

/// <summary>A job title an admin can pick for a barber. The list is filled by admins (and by titles already in use).</summary>
public class EmployeeTitle
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class EmployeeService
{
    public string EmployeeId { get; set; } = null!;
    public EmployeeProfile Employee { get; set; } = null!;
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
}

/// <summary>Weekly working hours in the salon's local time. One row per employee per day.</summary>
public class WorkingHours
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = null!;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
    public TimeOnly? BreakStart { get; set; }
    public TimeOnly? BreakEnd { get; set; }
}

public class TimeOff
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = null!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Single-row table (Id = 1) with salon-wide configuration.</summary>
public class SalonSettings
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = "Ottoman";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string TimeZoneId { get; set; } = "Europe/Sarajevo";
    public string Currency { get; set; } = "BAM";

    /// <summary>Internal booking granularity. Appointment durations must be multiples of this.</summary>
    public int SlotMinutes { get; set; } = 5;
    /// <summary>Grid used when offering start times to clients.</summary>
    public int OfferGridMinutes { get; set; } = 15;
    public int MinLeadMinutes { get; set; } = 30;
    public int CancelWindowMinutes { get; set; } = 120;

    /// <summary>
    /// On (default): a new booking is confirmed by the system at once. Off: it waits as Pending (its time held)
    /// until a barber or the admin confirms or declines it.
    /// </summary>
    public bool AutoConfirmReservations { get; set; } = true;
    public int LowStockDefault { get; set; } = 5;

    /// <summary>A few words about the salon, shown on the home screen.</summary>
    public string? Description { get; set; }

    /// <summary>A Google Maps link to the salon.</summary>
    public string? MapsUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? LogoKey { get; set; }

    /// <summary>Where the salon is, for the map preview on the home screen.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
