using FluentAssertions;
using Ottoman.Application.Accounts;

namespace Ottoman.Application.Tests;

public class AccountLockTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_lock_that_ends_in_the_future_is_shown_in_utc()
    {
        var end = new DateTimeOffset(2030, 1, 1, 13, 10, 0, TimeSpan.FromHours(1)); // 12:10 UTC
        AccountLock.LockedUntil(true, end, Now).Should().Be(new DateTime(2030, 1, 1, 12, 10, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void No_end_a_past_end_or_lockout_switched_off_means_not_locked()
    {
        AccountLock.LockedUntil(true, null, Now).Should().BeNull();
        AccountLock.LockedUntil(true, new DateTimeOffset(Now), Now).Should().BeNull();
        AccountLock.LockedUntil(true, new DateTimeOffset(Now.AddSeconds(-1)), Now).Should().BeNull();
        AccountLock.LockedUntil(false, new DateTimeOffset(Now.AddMinutes(15)), Now).Should().BeNull();
    }
}
