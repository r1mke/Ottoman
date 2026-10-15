using Microsoft.AspNetCore.Identity;

namespace Ottoman.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PhotoKey { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;

    public string FullName => $"{FirstName} {LastName}".Trim();
}
