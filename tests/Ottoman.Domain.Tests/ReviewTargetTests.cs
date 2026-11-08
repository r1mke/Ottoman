using FluentAssertions;
using Ottoman.Domain;
using Ottoman.Domain.Entities;

namespace Ottoman.Domain.Tests;

public class ReviewTargetTests
{
    [Theory]
    [InlineData(ReviewTargetType.Product, "12")]
    [InlineData(ReviewTargetType.Service, "7")]
    [InlineData(ReviewTargetType.Employee, "a1b2-user")]
    public void A_target_is_stored_in_exactly_one_typed_key_and_reads_back_as_the_same_key(ReviewTargetType type, string key)
    {
        var review = new Review();
        review.SetTarget(type, key);

        review.TargetType.Should().Be(type);
        review.TargetKey.Should().Be(key);
        new[] { review.ProductId is not null, review.ServiceId is not null, review.EmployeeProfileId is not null }
            .Count(set => set).Should().Be(1);
    }

    [Fact]
    public void Pointing_a_review_somewhere_else_clears_the_old_key()
    {
        var review = new Review();
        review.SetTarget(ReviewTargetType.Product, "12");
        review.SetTarget(ReviewTargetType.Service, "3");

        review.ProductId.Should().BeNull();
        review.ServiceId.Should().Be(3);
    }
}
