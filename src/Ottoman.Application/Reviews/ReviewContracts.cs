using FluentValidation;
using Ottoman.Application.Common;
using Ottoman.Domain;

namespace Ottoman.Application.Reviews;

public record CreateReviewRequest(ReviewTargetType TargetType, string TargetKey, int Rating, string? Comment);

public record UpdateReviewRequest(int Rating, string? Comment);

public record ReplyRequest(string Text);

public record HideRequest(bool Hidden);

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.TargetType).IsInEnum();
        RuleFor(x => x.TargetKey).NotEmpty().MaximumLength(450);
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

    Task<EligibilityDto> EligibilityAsync(Actor client, ReviewTargetType type, string key, CancellationToken ct);
    Task<Result<ReviewDto>> CreateAsync(Actor client, CreateReviewRequest request, CancellationToken ct);
    Task<Result<ReviewDto>> UpdateAsync(Actor client, int id, UpdateReviewRequest request, CancellationToken ct);
    Task<Result<bool>> DeleteAsync(Actor actor, int id, CancellationToken ct);

    Task<PagedResult<ReviewDto>> ListAllAsync(bool onlyHidden, PageQuery page, CancellationToken ct);
    Task<Result<ReviewDto>> SetHiddenAsync(int id, bool hidden, CancellationToken ct);
    Task<Result<ReviewDto>> ReplyAsync(int id, string text, CancellationToken ct);
}
