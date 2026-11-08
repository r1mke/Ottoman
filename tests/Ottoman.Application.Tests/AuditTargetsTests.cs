using FluentAssertions;
using Ottoman.Application.Audit;

namespace Ottoman.Application.Tests;

public class AuditTargetsTests
{
    [Theory]
    [InlineData("Products.Create", "Products")]
    [InlineData("Orders", "Orders")]
    public void The_controller_is_the_part_before_the_dot(string action, string expected) =>
        AuditTargets.Controller(action).Should().Be(expected);

    [Theory]
    [InlineData("id=7", "7")]
    [InlineData("id=7, timeOffId=3", "7")]
    [InlineData("timeOffId=3, id=a1b2", "a1b2")]
    [InlineData("", null)]
    [InlineData("id=", null)]
    [InlineData("timeOffId=3", null)]
    public void The_primary_id_is_the_id_of_the_record_the_request_was_about(string target, string? expected) =>
        AuditTargets.PrimaryId(target).Should().Be(expected);
}
