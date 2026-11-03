using FluentValidation;
using Ottoman.Application.Auth;

namespace Ottoman.Application.Salon;

/// <summary>Opening time of one weekday (0 = Sunday ... 6 = Saturday) as minutes after midnight; both null = closed.</summary>
public record OpeningHoursDto(int Day, int? OpenMinutes, int? CloseMinutes);

/// <summary>What the home screen shows about the salon. Readable by every signed-in person.</summary>
public record SalonProfileDto(
    string Name,
    string? Description,
    string? Address,
    string? Phone,
    string? MapsUrl,
    string? LogoUrl,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<OpeningHoursDto> Hours,
    string? InstagramUrl = null,
    string? FacebookUrl = null);

public record UpdateSalonProfileRequest(
    string Name,
    string? Description,
    string? Address,
    string? Phone,
    string? MapsUrl,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<OpeningHoursDto> Hours,
    string? InstagramUrl = null,
    string? FacebookUrl = null);

public class UpdateSalonProfileRequestValidator : AbstractValidator<UpdateSalonProfileRequest>
{
    public UpdateSalonProfileRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150).WithName("Naziv");
        RuleFor(x => x.Description).MaximumLength(1000).WithName("Opis");
        RuleFor(x => x.Address).MaximumLength(300).WithName("Adresa");
        RuleFor(x => x.Phone).BosnianPhone().WithName("Telefon");
        RuleFor(x => x.MapsUrl).MaximumLength(500)
            .Must(u => string.IsNullOrWhiteSpace(u) || IsWebUrl(u))
            .WithMessage("Unesite ispravnu web adresu (https://...).")
            .WithName("Google lokacija");
        RuleFor(x => x.InstagramUrl).MaximumLength(300)
            .Must(u => string.IsNullOrWhiteSpace(u) || IsWebUrl(u))
            .WithMessage("Unesite ispravnu web adresu (https://...).").WithName("Instagram");
        RuleFor(x => x.FacebookUrl).MaximumLength(300)
            .Must(u => string.IsNullOrWhiteSpace(u) || IsWebUrl(u))
            .WithMessage("Unesite ispravnu web adresu (https://...).").WithName("Facebook");
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude.HasValue).WithName("Geografska širina");
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude.HasValue).WithName("Geografska dužina");
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("Unesite i geografsku širinu i dužinu, ili nijednu.");
        RuleFor(x => x.Hours).NotNull()
            .Must(h => h is { Count: 7 } && h.Select(d => d.Day).Distinct().Count() == 7 && h.All(d => d.Day is >= 0 and <= 6))
            .WithMessage("Radno vrijeme mora imati svih 7 dana.");
        RuleForEach(x => x.Hours).Must(d =>
                (d.OpenMinutes is null && d.CloseMinutes is null) ||
                (d.OpenMinutes is >= 0 && d.CloseMinutes is <= 1440 && d.OpenMinutes < d.CloseMinutes))
            .WithMessage("Vrijeme otvaranja mora biti prije vremena zatvaranja.");
    }

    private static bool IsWebUrl(string u) =>
        Uri.TryCreate(u.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public interface ISalonProfileService
{
    Task<SalonProfileDto> GetAsync(CancellationToken ct);
    Task<SalonProfileDto> UpdateAsync(UpdateSalonProfileRequest request, CancellationToken ct);
}
