namespace Ottoman.Infrastructure.Seeding;

public class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Demo employees/services/products/client. Never enable in production.</summary>
    public bool DemoData { get; set; }
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string DemoPassword { get; set; } = "Test123!";
}
