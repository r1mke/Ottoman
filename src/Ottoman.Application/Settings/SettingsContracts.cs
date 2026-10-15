using FluentValidation;
using Ottoman.Application.Common;

namespace Ottoman.Application.Settings;

public record SalonSettingsDto(
    string Name,
    string? Address,
    string? Phone,
    string TimeZoneId,
    string Currency,
    int SlotMinutes,
    int OfferGridMinutes,
    int MinLeadMinutes,
    int CancelWindowMinutes,
    int LowStockDefault);

/// <summary>SlotMinutes is fixed at 5 and not editable: service durations are multiples of 5.</summary>
public record UpdateSalonSettingsRequest(
    string Name,
    string? Address,
    string? Phone,
    string TimeZoneId,
    string Currency,
    int OfferGridMinutes,
    int MinLeadMinutes,
    int CancelWindowMinutes,
    int LowStockDefault);

public class UpdateSalonSettingsRequestValidator : AbstractValidator<UpdateSalonSettingsRequest>
{
    public UpdateSalonSettingsRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.TimeZoneId).NotEmpty().Must(IsKnownZone).WithMessage("Nepoznata vremenska zona.");
        RuleFor(x => x.Currency).Length(3).Matches("^[A-Z]{3}$");
        RuleFor(x => x.OfferGridMinutes).InclusiveBetween(5, 60).Must(g => g % 5 == 0)
            .WithMessage("Mreža termina mora biti višekratnik od 5 minuta.");
        RuleFor(x => x.MinLeadMinutes).InclusiveBetween(0, 24 * 60);
        RuleFor(x => x.CancelWindowMinutes).InclusiveBetween(0, 7 * 24 * 60);
        RuleFor(x => x.LowStockDefault).InclusiveBetween(0, 10_000);
    }

    private static bool IsKnownZone(string id)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}

public interface ISalonSettingsService
{
    Task<SalonSettingsDto> GetAsync(CancellationToken ct);
    Task<SalonSettingsDto> UpdateAsync(UpdateSalonSettingsRequest request, CancellationToken ct);
}
