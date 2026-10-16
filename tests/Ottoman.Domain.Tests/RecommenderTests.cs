using FluentAssertions;
using Ottoman.Domain.Recommendations;

namespace Ottoman.Domain.Tests;

public class TextNormalizerTests
{
    [Fact]
    public void Diacritics_case_and_stop_words_are_removed()
    {
        TextNormalizer.Tokens("Šišanje i ĆEVAPI za đaka!").Should().Equal("sisanje", "cevapi", "daka");
    }

    [Fact]
    public void Words_typed_with_and_without_diacritics_match()
    {
        TextNormalizer.Tokens("brada").Should().Equal(TextNormalizer.Tokens("BRADA"));
        TextNormalizer.Tokens("šampon ž").Should().Equal(TextNormalizer.Tokens("sampon"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("i a u")]
    public void Nothing_meaningful_gives_no_tokens(string? text) => TextNormalizer.Tokens(text).Should().BeEmpty();

    [Fact]
    public void Digits_are_kept_and_punctuation_splits() =>
        TextNormalizer.Tokens("Vosak-250ml, jaka fiksacija").Should().Equal("vosak", "250ml", "jaka", "fiksacija");
}

public class SparseVectorTests
{
    private static SparseVector V(params (string, double)[] w) => new(w.ToDictionary(x => x.Item1, x => x.Item2));

    [Fact]
    public void Identical_directions_have_cosine_one_whatever_the_length() =>
        V(("a", 1), ("b", 2)).Cosine(V(("a", 10), ("b", 20))).Should().BeApproximately(1, 1e-9);

    [Fact]
    public void Unrelated_vectors_have_cosine_zero() => V(("a", 1)).Cosine(V(("b", 1))).Should().Be(0);

    [Fact]
    public void Opposite_vectors_have_cosine_minus_one() =>
        V(("a", 1)).Cosine(V(("a", -3))).Should().BeApproximately(-1, 1e-9);

    [Fact]
    public void An_empty_vector_is_similar_to_nothing() => SparseVector.Empty.Cosine(V(("a", 1))).Should().Be(0);
}

public class RecommenderTests
{
    private static readonly DateTime Now = new(2030, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlySet<int> None = new HashSet<int>();

    // ------------------------------------------------------------- services

    private static readonly ItemFeatures[] Services =
    [
        new(1, "Fade šišanje Klasični fade s makazama i mašinicom"),
        new(2, "Taper fade Fade šišanje s postepenim prijelazom"),
        new(3, "Uređivanje brade Oblikovanje i njega brade"),
        new(4, "Masaža glave Opuštajuća masaža"),
    ];

    private static IReadOnlySet<int> All(IEnumerable<ItemFeatures> items) => items.Select(i => i.Id).ToHashSet();

    [Fact]
    public void Someone_who_liked_a_fade_is_offered_the_taper_fade_first()
    {
        var index = ItemIndex.Build(Services);
        var likes = new[] { new Interaction(1, 1.0, Now.AddDays(-5)) };

        var top = Recommender.ForUser(index, likes, Now, All(Services), new HashSet<int> { 1 }, [], 3);

        top.First().Id.Should().Be(2);
        top.Select(t => t.Id).Should().NotContain(1, "they already had it");
    }

    [Fact]
    public void Beard_care_taste_leads_to_beard_services()
    {
        var index = ItemIndex.Build(Services);
        var top = Recommender.ForUser(index, [new Interaction(3, 1.0, Now)], Now, All(Services), new HashSet<int> { 3 }, [], 3);
        top.Should().NotContain(s => s.Id == 3);
        top.Select(s => s.Id).Should().NotContain(2, "a fade has nothing to do with a beard trim");
    }

    // ------------------------------------------------------------- products

    private static readonly ItemFeatures[] Products =
    [
        new(10, "Dandy Beard Oil ulje za njegu brade", "Njega brade", "Dandy", 14m),
        new(11, "Proraso Beard Balm balzam za bradu", "Njega brade", "Proraso", 16m),
        new(12, "Dandy Shaping Pomade vosak za kosu", "Voskovi", "Dandy", 10m),
        new(13, "Dandy Matt Clay glina za kosu", "Voskovi", "Dandy", 12m),
        new(14, "Šampon protiv peruti", "Šamponi", "Nivea", 8m),
    ];

    [Fact]
    public void Beard_oil_is_followed_by_beard_balm_then_other_Dandy_products()
    {
        var index = ItemIndex.Build(Products);
        var top = Recommender.ForUser(index, [new Interaction(10, 1.0, Now)], Now, All(Products), new HashSet<int> { 10 }, [], 4);

        top.Select(t => t.Id).Should().StartWith([11], "same category outranks same brand");
        top.Select(t => t.Id).Take(3).Should().BeEquivalentTo([11, 12, 13]);
        top.Select(t => t.Id).Should().NotContain(14, "an unrelated shampoo scores zero and is not offered");
    }

    [Fact]
    public void Related_products_for_a_pomade_are_other_waxes_of_the_same_brand()
    {
        var index = ItemIndex.Build(Products);
        var related = Recommender.Related(index, 12, All(Products), 3);

        related.First().Id.Should().Be(13);
        related.Select(r => r.Id).Should().NotContain(12);
    }

    [Fact]
    public void Out_of_stock_products_are_never_offered()
    {
        var index = ItemIndex.Build(Products);
        var inStock = new HashSet<int> { 10, 12, 14 }; // balm and clay are sold out
        var top = Recommender.ForUser(index, [new Interaction(10, 1.0, Now)], Now, inStock, new HashSet<int> { 10 }, [], 5);
        top.Select(t => t.Id).Should().NotContain([11, 13]);
        Recommender.Related(index, 12, inStock, 5).Select(r => r.Id).Should().NotContain([11, 13]);
    }

    [Fact]
    public void A_disliked_product_pushes_its_neighbours_down_and_out()
    {
        var index = ItemIndex.Build(Products);
        // loved the pomade, hated the clay (same brand and category): the clay and similar should not lead
        var history = new[]
        {
            new Interaction(12, Recommender.ReviewWeight(5), Now),
            new Interaction(13, Recommender.ReviewWeight(1) * 3, Now),
        };
        var top = Recommender.ForUser(index, history, Now, All(Products), new HashSet<int> { 12, 13 }, [], 5);
        top.Select(t => t.Id).Should().NotContain(13);
    }

    [Fact]
    public void Recent_behaviour_outweighs_old_behaviour()
    {
        var index = ItemIndex.Build(Products);
        var history = new[]
        {
            new Interaction(12, 1.0, Now.AddDays(-720)), // waxes, two years ago
            new Interaction(10, 1.0, Now.AddDays(-2)),   // beard care, this week
        };
        var top = Recommender.ForUser(index, history, Now, All(Products), new HashSet<int> { 10, 12 }, [], 1);
        top.Single().Id.Should().Be(11);
    }

    // ------------------------------------------------------------- cold start & edges

    [Fact]
    public void A_new_user_gets_the_popular_list_in_its_order_filtered_by_availability()
    {
        var index = ItemIndex.Build(Products);
        var popular = new[] { 13, 10, 12, 14 };
        var top = Recommender.ForUser(index, [], Now, new HashSet<int> { 10, 12, 14 }, None, popular, 2);
        top.Select(t => t.Id).Should().Equal(10, 12);
    }

    [Fact]
    public void Cold_start_also_applies_when_history_matches_nothing_in_the_catalogue()
    {
        var index = ItemIndex.Build(Products);
        var top = Recommender.ForUser(index, [new Interaction(999, 1.0, Now)], Now, All(Products), None, [14, 10], 2);
        top.Select(t => t.Id).Should().Equal(14, 10);
    }

    [Fact]
    public void Nothing_is_returned_when_there_is_nothing_to_show()
    {
        var index = ItemIndex.Build(Products);
        Recommender.ForUser(index, [], Now, None, None, [10], 3).Should().BeEmpty();
        Recommender.Related(index, 999, All(Products), 3).Should().BeEmpty();
    }

    [Fact]
    public void The_popular_fallback_skips_items_that_resemble_something_the_user_disliked()
    {
        var index = ItemIndex.Build(Products);
        // bought a wax (+1) and rated it 1 star (-2): net negative, so no positive match exists
        var history = new[] { new Interaction(12, 1.0, Now), new Interaction(12, Recommender.ReviewWeight(1), Now) };
        var top = Recommender.ForUser(index, history, Now, All(Products), new HashSet<int> { 12 }, [13, 10, 11], 5);
        // 13 is another wax, 10 shares the brand: both resemble the disliked item; the balm does not
        top.Select(t => t.Id).Should().Equal([11]);
    }

    [Fact]
    public void The_same_input_always_gives_the_same_output()
    {
        var index = ItemIndex.Build(Products);
        var h = new[] { new Interaction(10, 1.0, Now) };
        var a = Recommender.ForUser(index, h, Now, All(Products), new HashSet<int> { 10 }, [], 4);
        var b = Recommender.ForUser(ItemIndex.Build(Products.Reverse().ToList()), h, Now, All(Products), new HashSet<int> { 10 }, [], 4);
        a.Select(x => x.Id).Should().Equal(b.Select(x => x.Id));
    }

    [Fact]
    public void Count_limits_the_result()
    {
        var index = ItemIndex.Build(Products);
        Recommender.ForUser(index, [new Interaction(10, 1.0, Now)], Now, All(Products), None, [], 2).Should().HaveCount(2);
    }

    // ------------------------------------------------------------- building blocks

    [Fact]
    public void Decay_halves_every_ninety_days()
    {
        Recommender.Decay(Now, Now).Should().BeApproximately(1, 1e-9);
        Recommender.Decay(Now.AddDays(-90), Now).Should().BeApproximately(0.5, 1e-9);
        Recommender.Decay(Now.AddDays(-180), Now).Should().BeApproximately(0.25, 1e-9);
        Recommender.Decay(Now.AddDays(5), Now).Should().Be(1, "a future timestamp is clamped, not amplified");
    }

    [Theory]
    [InlineData(5, 1.5)]
    [InlineData(4, 1.5)]
    [InlineData(3, 0)]
    [InlineData(2, -2)]
    [InlineData(1, -2)]
    public void Review_weights(int rating, double weight) => Recommender.ReviewWeight(rating).Should().Be(weight);

    [Fact]
    public void Rare_words_matter_more_than_common_ones()
    {
        var items = new ItemFeatures[]
        {
            new(1, "vosak kosa"), new(2, "vosak kosa"), new(3, "vosak kosa"), new(4, "vosak brada"),
        };
        var index = ItemIndex.Build(items);
        // item 4 shares only the common word "vosak" with 1; item 2 shares everything
        index.Vectors[1].Cosine(index.Vectors[2]).Should().BeGreaterThan(index.Vectors[1].Cosine(index.Vectors[4]));
    }
}
