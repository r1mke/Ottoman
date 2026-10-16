using FluentAssertions;
using Ottoman.Domain.Reservations;

namespace Ottoman.Domain.Tests;

public class CancellationRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Staff_cannot_cancel_without_a_real_reason(string? reason)
    {
        CancellationRules.ReasonProblem(ActorKind.Admin, reason).Should().Contain("razlog");
        CancellationRules.ReasonProblem(ActorKind.Employee, reason).Should().Contain("razlog");
    }

    [Theory]
    [InlineData("Frizer je bolestan")]
    [InlineData("  abc  ")]
    public void A_reason_of_a_few_words_is_enough(string reason)
    {
        CancellationRules.ReasonProblem(ActorKind.Admin, reason).Should().BeNull();
        CancellationRules.ReasonProblem(ActorKind.Employee, reason).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Predomislio sam se")]
    public void A_client_may_cancel_with_or_without_explaining(string? reason) =>
        CancellationRules.ReasonProblem(ActorKind.Client, reason).Should().BeNull();
}
