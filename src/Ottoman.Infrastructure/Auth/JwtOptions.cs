using System.ComponentModel.DataAnnotations;

namespace Ottoman.Infrastructure.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "ottoman";

    [Required]
    public string Audience { get; set; } = "ottoman-clients";

    /// <summary>HMAC key. Must come from environment/secret store, never from source control.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    [Range(1, 120)]
    public int AccessTokenMinutes { get; set; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}
