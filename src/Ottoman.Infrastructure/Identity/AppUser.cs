using Microsoft.AspNetCore.Identity;

namespace Ottoman.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PhotoKey { get; set; }

    /// <summary>
    /// How close the account's own picture (a client's, the admin's) crops around the face: 1 = Cloudinary's default,
    /// 2 = twice as close. A barber's lives on the barber profile next to that picture.
    /// </summary>
    public double PhotoZoom { get; set; } = 1.0;

    /// <summary>Gender and bio of an account without a barber profile (the admin); a barber's live on the profile.</summary>
    public Ottoman.Domain.Gender? Gender { get; set; }

    public string? Bio { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
