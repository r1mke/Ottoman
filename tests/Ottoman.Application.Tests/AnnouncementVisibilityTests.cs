using FluentAssertions;
using Ottoman.Application.Notifications;

namespace Ottoman.Application.Tests;

public class AnnouncementVisibilityTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Visible_unless_hidden_or_past_its_end()
    {
        AnnouncementVisibility.Visible(false, null, Now).Should().BeTrue();
        AnnouncementVisibility.Visible(false, Now.AddMinutes(1), Now).Should().BeTrue();
        AnnouncementVisibility.Visible(false, Now, Now).Should().BeFalse("the end moment itself is already over");
        AnnouncementVisibility.Visible(true, null, Now).Should().BeFalse();
    }

    [Fact]
    public void Expired_only_with_an_end_date_that_passed()
    {
        AnnouncementVisibility.Expired(null, Now).Should().BeFalse();
        AnnouncementVisibility.Expired(Now.AddDays(-1), Now).Should().BeTrue();
        AnnouncementVisibility.Expired(Now.AddDays(1), Now).Should().BeFalse();
    }
}
