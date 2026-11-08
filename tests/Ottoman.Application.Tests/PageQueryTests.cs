using FluentAssertions;
using Ottoman.Application.Common;

namespace Ottoman.Application.Tests;

public class PageQueryTests
{
    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(3, 20, 40)]
    [InlineData(0, 20, 0)]
    [InlineData(-5, 0, 0)]
    [InlineData(2, 1000, 100)]
    public void Skip_follows_the_safe_page_and_size(int page, int size, int skip) =>
        new PageQuery(page, size).Skip.Should().Be(skip);

    [Fact]
    public void A_huge_page_number_never_overflows_into_a_negative_offset()
    {
        var q = new PageQuery(int.MaxValue, int.MaxValue);
        q.SafePage.Should().Be(PageQuery.MaxPage);
        q.Skip.Should().Be((PageQuery.MaxPage - 1) * PageQuery.MaxPageSize).And.BePositive();
    }
}
