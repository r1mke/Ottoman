namespace Ottoman.Domain.Entities;

/// <summary>Employee-specific data. Shares its key with the Identity user (1:1).</summary>
public class EmployeeProfile
{
    public string UserId { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Bio { get; set; }
    public string? PhotoKey { get; set; }
    public bool IsActive { get; set; } = true;

    public List<EmployeeService> Services { get; set; } = [];
    public List<WorkingHours> WorkingHours { get; set; } = [];
    public List<TimeOff> TimeOff { get; set; } = [];
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
    public int LowStockDefault { get; set; } = 5;
}
