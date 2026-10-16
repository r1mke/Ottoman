using FluentAssertions;
using Ottoman.Application.Audit;

namespace Ottoman.Application.Tests;

public class AuditPolicyTests
{
    [Theory]
    [InlineData("POST", "Products", 201)]
    [InlineData("PUT", "Services", 200)]
    [InlineData("DELETE", "Products", 204)]
    [InlineData("POST", "Clients", 200)]
    public void Successful_staff_writes_are_recorded(string method, string controller, int status) =>
        AuditPolicy.ShouldRecord(method, controller, status, isStaff: true).Should().BeTrue();

    [Theory]
    [InlineData("GET", "Products", 200)]
    [InlineData("HEAD", "Products", 200)]
    [InlineData("OPTIONS", "Products", 204)]
    public void Reads_are_not_recorded(string method, string controller, int status) =>
        AuditPolicy.ShouldRecord(method, controller, status, isStaff: true).Should().BeFalse();

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(500)]
    public void Failed_requests_changed_nothing_so_they_are_not_recorded(int status) =>
        AuditPolicy.ShouldRecord("POST", "Products", status, isStaff: true).Should().BeFalse();

    [Fact]
    public void Clients_are_not_staff() =>
        AuditPolicy.ShouldRecord("POST", "Orders", 201, isStaff: false).Should().BeFalse();

    [Theory]
    [InlineData("Notifications")]
    [InlineData("Devices")]
    [InlineData("Auth")]
    [InlineData("Me")]
    [InlineData("Payments")]
    [InlineData("notifications")]
    public void Noise_and_sign_in_are_ignored(string controller) =>
        AuditPolicy.ShouldRecord("POST", controller, 204, isStaff: true).Should().BeFalse();

    [Fact]
    public void Entries_are_kept_for_a_year() => AuditPolicy.Retention.Should().Be(TimeSpan.FromDays(365));

}
