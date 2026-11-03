using FluentValidation;
using Ottoman.Application.Common;
using Ottoman.Domain;

namespace Ottoman.Application.Reviews;

public record CreateReviewRequest(ReviewTargetType TargetType, string TargetKey, int Rating, string? Comment);

public record UpdateReviewRequest(int Rating, string? Comment);

public record ReplyRequest(string Text);

/// <summary>Hidden is required: a request without it must not quietly un-hide a review.</summary>
public record HideRequest(bool? Hidden);

public class HideRequestValidator : AbstractValidator<HideRequest>
{
    public HideRequestValidator() =>
        RuleFor(x => x.Hidden).NotNull().WithMessage("Navedite da li se recenzija skriva ili prikazuje.").WithName("Skriveno");
}

/// <summary>
/// The one spelling of a review target. Services and products are keyed by their number exactly as the server
/// writes it ("5", never "05", " 5" or "+5"), so one client cannot review the same thing twice under different
/// spellings; employees by their account id as is.
/// </summary>
public static class ReviewTargetKey
{
    /// <summary>The canonical key, or null when <paramref name="key"/> is not one.</summary>
    public static string? Canonical(ReviewTargetType type, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        switch (type)
        {
            case ReviewTargetType.Service:
            case ReviewTargetType.Product:
                return int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id)
                       && id > 0 && id.ToString(System.Globalization.CultureInfo.InvariantCulture) == key
                    ? key
                    : null;
            case ReviewTargetType.Employee:
                return key.Trim() == key ? key : null;
            default:
                return null;
        }
    }
}

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.TargetType).IsInEnum();
        RuleFor(x => x.TargetKey).NotEmpty().MaximumLength(450);
        RuleFor(x => x.TargetKey).Must((r, key) => ReviewTargetKey.Canonical(r.TargetType, key) is not null)
            .When(x => !string.IsNullOrEmpty(x.TargetKey))
            .WithMessage("Neispravna oznaka stavke.").WithName("Stavka");
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithName("Ocjena");
        RuleFor(x => x.Comment).MaximumLength(1000).WithName("Komentar");
    }
}

public class UpdateReviewRequestValidator : AbstractValidator<UpdateReviewRequest>
{
    public UpdateReviewRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithName("Ocjena");
        RuleFor(x => x.Comment).MaximumLength(1000).WithName("Komentar");
    }
}

public class ReplyRequestValidator : AbstractValidator<ReplyRequest>
{
    public ReplyRequestValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(1000).WithName("Odgovor");
}

public record ReviewDto(
    int Id,
    ReviewTargetType TargetType,
    string TargetKey,
    string TargetName,
    string AuthorName,
    int Rating,
    string? Comment,
    DateTime CreatedAtUtc,
    bool IsHidden,
    string? ReplyText,
    DateTime? ReplyAtUtc,
    bool IsMine);

public record ReviewSummaryDto(string TargetKey, double Average, int Count);

public record ReviewListDto(IReadOnlyList<ReviewDto> Items, int TotalCount, double Average, int Count);

/// <param name="Reason">Why not, in words for the client.</param>
public record EligibilityDto(bool CanReview, string? Reason, int? ExistingReviewId);

public interface IReviewService
{
    /// <summary>Visible reviews of one target with their average. Admins may also see hidden ones.</summary>
    Task<ReviewListDto> ListAsync(Actor viewer, ReviewTargetType type, string key, bool includeHidden, PageQuery page, CancellationToken ct);

    /// <summary>Average and count per target of one kind (hidden reviews do not count).</summary>
    Task<IReadOnlyList<ReviewSummaryDto>> SummaryAsync(ReviewTargetType type, CancellationToken ct);
    Task<PagedResult<ReviewSummaryDto>> SummaryPageAsync(ReviewTargetType type, PageQuery page, CancellationToken ct);

    Task<EligibilityDto> EligibilityAsync(Actor client, ReviewTargetType type, string key, CancellationToken ct);
    Task<Result<ReviewDto>> CreateAsync(Actor client, CreateReviewRequest request, CancellationToken ct);
    Task<Result<ReviewDto>> UpdateAsync(Actor client, int id, UpdateReviewRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteAsync(Actor actor, int id, CancellationToken ct);

    Task<PagedResult<ReviewDto>> ListAllAsync(bool onlyHidden, PageQuery page, CancellationToken ct);
    Task<Result<ReviewDto>> SetHiddenAsync(int id, bool hidden, CancellationToken ct);
    Task<Result<ReviewDto>> ReplyAsync(int id, string text, CancellationToken ct);
}
