using FluentAssertions;

namespace Ottoman.Domain.Tests;

public class RolesTests
{
    [Fact]
    public void All_contains_exactly_the_three_application_roles()
    {
        Roles.All.Should().BeEquivalentTo(["Admin", "Employee", "Client"]);
    }
}
