namespace Ottoman.Application.Common;

/// <summary>
/// Refuses to start a production instance with the kinds of settings that are fine on a laptop and dangerous on a
/// server: placeholder secrets copied from an example file, demo accounts with known passwords.
/// Pure so it can be tested; the host decides what to do with the problems (it stops).
/// </summary>
public static class StartupGuards
{
    private static readonly string[] Placeholders = ["change-me", "changeme", "password", "secret", "example", "your-key", "xxxx"];

    public static IReadOnlyList<string> Problems(
        bool isDevelopment,
        string? jwtSigningKey,
        bool seedDemoData,
        string? seedAdminPassword)
    {
        if (isDevelopment) return [];

        var problems = new List<string>();

        var key = jwtSigningKey ?? "";
        if (Placeholders.Any(p => key.Contains(p, StringComparison.OrdinalIgnoreCase)))
            problems.Add("Jwt:SigningKey looks like a placeholder from an example file. Generate a real one (openssl rand -base64 48).");
        else if (key.Distinct().Count() < 10)
            problems.Add("Jwt:SigningKey has too little variety to be a real secret. Generate one (openssl rand -base64 48).");

        if (seedDemoData)
            problems.Add("Seed:DemoData must be false in production: it creates accounts with well-known passwords.");

        if (!string.IsNullOrEmpty(seedAdminPassword) &&
            Placeholders.Any(p => seedAdminPassword.Contains(p, StringComparison.OrdinalIgnoreCase)))
            problems.Add("Seed:AdminPassword is a placeholder. Set a real password before the first start.");

        return problems;
    }
}
