using FluentAssertions;
using Ottoman.Domain.Recommendations;

namespace Ottoman.Domain.Tests;

public class RecommenderExplanationTests
{
    private static readonly DateTime Now = new(2030, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlySet<int> None = new HashSet<int>();

    private static readonly ItemFeatures[] Products =
    [
        new(10, "Dandy Beard Oil ulje za njegu brade", "Njega brade", "Dandy", 14m),
        new(11, "Proraso Beard Balm balzam za bradu", "Njega brade", "Proraso", 16m),
        new(12, "Dandy Shaping Pomade vosak za kosu", "Voskovi", "Dandy", 10m),
        new(13, "Dandy Matt Clay glina za kosu", "Voskovi", "Dandy", 12m),
        new(14, "Šampon protiv peruti", "Šamponi", "Nivea", 8m),
    ];

    private static IReadOnlySet<int> All => Products.Select(p => p.Id).ToHashSet();

    [Fact]
    public void Every_recommendation_names_the_item_it_came_from_and_what_they_share()
    {
        var index = ItemIndex.Build(Products);

        var top = Recommender.ForUser(index, [new Interaction(10, 1.0, Now)], Now, All, new HashSet<int> { 10 }, [], 4);

        var balm = top.Single(t => t.Id == 11);
        balm.Reason.Should().NotBeNull();
        balm.Reason!.Kind.Should().Be(ReasonKind.SimilarToInterest);
        balm.Reason.SourceItemId.Should().Be(10, "the beard oil is what the balm resembles");
        balm.Reason.SharedCategory.Should().Be("Njega brade");
        balm.Reason.SharedBrand.Should().BeNull("the brands differ, so the explanation must not claim it");

        var pomade = top.Single(t => t.Id == 12);
        pomade.Reason!.SharedBrand.Should().Be("Dandy");
        pomade.Reason.SharedCategory.Should().BeNull();
    }

    [Fact]
    public void The_reason_points_at_the_liked_item_that_pulled_hardest()
    {
        var index = ItemIndex.Build(Products);
        // The wax was bought long ago, the oil today: for the balm the oil counts, for the clay the (recent) wax would not.
        var history = new[]
        {
            new Interaction(12, 1.0, Now.AddDays(-400)),
            new Interaction(10, 1.0, Now),
        };

        var top = Recommender.ForUser(index, history, Now, All, new HashSet<int> { 10, 12 }, [], 4);

        top.Single(t => t.Id == 11).Reason!.SourceItemId.Should().Be(10);
    }

    [Fact]
    public void Shared_words_are_shown_as_people_wrote_them_not_as_stripped_tokens()
    {
        var items = new ItemFeatures[]
        {
            new(1, "Šišanje fade klasično"),
            new(2, "Fade šišanje moderno"),
            new(3, "Masaža glave"),
        };
        var index = ItemIndex.Build(items);

        var reason = index.Explain(1, 2, ReasonKind.SimilarToInterest);

        reason.SharedWords.Should().Contain("šišanje", "with the diacritics the person would type");
        reason.SharedWords.Should().Contain("fade");
        reason.SharedWords.Should().NotContain("sisanje");
    }

    [Fact]
    public void Disliked_items_are_never_given_as_the_reason()
    {
        var index = ItemIndex.Build(Products);
        // Hated the oil, liked the pomade: the oil must not be named as the reason for anything.
        var history = new[] { new Interaction(10, -2.0, Now), new Interaction(12, 1.5, Now) };

        var top = Recommender.ForUser(index, history, Now, All, new HashSet<int> { 10, 12 }, [], 4);

        top.Should().NotBeEmpty();
        top.Where(t => t.Reason!.Kind == ReasonKind.SimilarToInterest).Should().OnlyContain(t => t.Reason!.SourceItemId == 12);
    }

    [Fact]
    public void Without_history_the_reason_is_honestly_popularity()
    {
        var index = ItemIndex.Build(Products);

        var top = Recommender.ForUser(index, [], Now, All, None, [12, 13, 11], 3);

        top.Should().HaveCount(3);
        top.Should().OnlyContain(t => t.Reason!.Kind == ReasonKind.Popular && t.Reason.SourceItemId == null);
    }

    [Fact]
    public void Similar_items_name_the_item_being_viewed()
    {
        var index = ItemIndex.Build(Products);

        var related = Recommender.Related(index, 12, All, 3);

        related.Should().OnlyContain(r => r.Reason!.Kind == ReasonKind.SimilarToItem && r.Reason.SourceItemId == 12);
        related.First(r => r.Id == 13).Reason!.SharedCategory.Should().Be("Voskovi");
        related.First(r => r.Id == 13).Reason!.SharedBrand.Should().Be("Dandy");
    }

    [Fact]
    public void Explaining_changes_nothing_about_the_ranking()
    {
        var index = ItemIndex.Build(Products);
        var history = new[] { new Interaction(10, 1.0, Now) };

        var a = Recommender.ForUser(index, history, Now, All, new HashSet<int> { 10 }, [], 4);
        var b = Recommender.ForUser(index, history, Now, All, new HashSet<int> { 10 }, [], 4);

        a.Select(x => (x.Id, x.Score)).Should().Equal(b.Select(x => (x.Id, x.Score)));
    }
}
