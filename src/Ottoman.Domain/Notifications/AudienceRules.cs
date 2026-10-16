namespace Ottoman.Domain.Notifications;

public static class AudienceRules
{
    /// <summary>The roles whose members receive a message for this audience. Empty for a single named user.</summary>
    public static IReadOnlyList<string> RolesFor(AnnouncementAudience audience) => audience switch
    {
        AnnouncementAudience.All => [Roles.Client, Roles.Employee, Roles.Admin],
        AnnouncementAudience.Clients => [Roles.Client],
        AnnouncementAudience.Employees => [Roles.Employee],
        _ => [],
    };
}
