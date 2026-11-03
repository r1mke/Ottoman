using FluentAssertions;
using Ottoman.Application.Reviews;
using Ottoman.Domain;

namespace Ottoman.Application.Tests;

public class ReviewTargetKeyTests
{
    [Theory]
    [InlineData("5")]
    [InlineData("1234")]
    public void A_plain_number_is_the_key_of_a_service_or_product(string key)
    {
        ReviewTargetKey.Canonical(ReviewTargetType.Service, key).Should().Be(key);
        ReviewTargetKey.Canonical(ReviewTargetType.Product, key).Should().Be(key);
    }

    [Theory]
    [InlineData("05")]
    [InlineData(" 5")]
    [InlineData("5 ")]
    [InlineData("+5")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("5.0")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void Other_spellings_of_a_number_are_refused(string? key)
    {
        ReviewTargetKey.Canonical(ReviewTargetType.Service, key).Should().BeNull();
        ReviewTargetKey.Canonical(ReviewTargetType.Product, key).Should().BeNull();
    }

    [Fact]
    public void An_employee_is_keyed_by_the_account_id_as_is()
    {
        ReviewTargetKey.Canonical(ReviewTargetType.Employee, "8f6c2a").Should().Be("8f6c2a");
        ReviewTargetKey.Canonical(ReviewTargetType.Employee, " 8f6c2a").Should().BeNull();
    }
}
