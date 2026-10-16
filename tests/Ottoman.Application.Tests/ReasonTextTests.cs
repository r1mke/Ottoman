using FluentAssertions;
using Ottoman.Application.Recommendations;
using Ottoman.Domain.Recommendations;

namespace Ottoman.Application.Tests;

public class ReasonTextTests
{
    private static Reason Similar(string? category = null, string? brand = null, bool band = false, params string[] words) =>
        new(ReasonKind.SimilarToInterest, 1, category, brand, band, words);

    [Fact]
    public void A_product_reason_names_what_was_bought_and_what_is_shared()
    {
        var r = ReasonText.Compose(Similar("Voskovi", "Dandy"), "Dandy Pomade", RecommendedKind.Product);

        r.Text.Should().Be("Jer ste kupili „Dandy Pomade“ - ista kategorija (Voskovi), isti brend (Dandy)");
        r.Kind.Should().Be("SimilarToInterest");
        r.SourceName.Should().Be("Dandy Pomade");
    }

    [Theory]
    [InlineData(RecommendedKind.Service, "Jer ste koristili uslugu „Fade“")]
    [InlineData(RecommendedKind.Barber, "Jer ste bili kod frizera Fade")]
    [InlineData(RecommendedKind.Product, "Jer ste kupili „Fade“")]
    public void The_opening_fits_what_is_recommended(RecommendedKind kind, string expected) =>
        ReasonText.Compose(Similar(), "Fade", kind).Text.Should().Be(expected);

    [Fact]
    public void Only_facts_that_were_found_are_stated()
    {
        ReasonText.Compose(Similar(words: ["šišanje", "fade"]), "Fade šišanje", RecommendedKind.Service).Text
            .Should().Be("Jer ste koristili uslugu „Fade šišanje“ - slični pojmovi: šišanje, fade")
            .And.NotContain("kategorija").And.NotContain("brend");

        ReasonText.Compose(Similar(band: true), "X", RecommendedKind.Product).Text
            .Should().Be("Jer ste kupili „X“ - slična cijena");
    }

    [Fact]
    public void Similar_items_say_what_they_are_similar_to()
    {
        var r = ReasonText.Compose(new Reason(ReasonKind.SimilarToItem, 5, "Njega brade", null, false, []), "Ulje za bradu", RecommendedKind.Product);

        r.Text.Should().Be("Slično kao „Ulje za bradu“ - ista kategorija (Njega brade)");
    }

    [Fact]
    public void Popularity_is_said_plainly_and_a_vanished_source_does_not_break_the_sentence()
    {
        ReasonText.Compose(Reason.Popular, null, RecommendedKind.Product).Text.Should().StartWith("Popularno u salonu");
        ReasonText.Compose(Similar("Voskovi"), null, RecommendedKind.Product).Text
            .Should().Be("Slično onome što ste već birali - ista kategorija (Voskovi)");
    }
}
