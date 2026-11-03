using Microsoft.AspNetCore.Identity;

namespace Ottoman.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PhotoKey { get; set; }

    /// <summary>Gender and bio of an account without a barber profile (the admin); a barber's live on the profile.</summary>
    public Ottoman.Domain.Gender? Gender { get; set; }

    public string? Bio { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
