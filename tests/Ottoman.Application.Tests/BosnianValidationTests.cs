using FluentAssertions;
using FluentValidation;
using Ottoman.Application.Common;

namespace Ottoman.Application.Tests;

public class BosnianValidationTests
{
    public sealed record Sample(string? Name, string? Reason, string? Email, int Quantity, decimal Price, string? Code, string? Note, int? Count);

    /// <summary>The common rules with FluentValidation's own messages (no WithMessage), as most simple rules use them.</summary>
    private sealed class SampleValidator : AbstractValidator<Sample>
    {
        public SampleValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.Reason).Length(3, 10);
            RuleFor(x => x.Email).EmailAddress();
            RuleFor(x => x.Quantity).InclusiveBetween(1, 5);
            RuleFor(x => x.Price).GreaterThan(0);
            RuleFor(x => x.Code).Matches("^[0-9]+$");
            RuleFor(x => x.Note).MaximumLength(4);
            RuleFor(x => x.Count).NotNull().LessThan(3);
        }
    }

    private static readonly string[] English =
        ["must", "should", "empty", "characters", "is not", "a valid", "between", "greater", "less than", "correct format", "specified", "Reason", "Quantity", "Price", "Code", "Note"];

    [Fact]
    public void Default_messages_of_the_common_rules_are_bosnian_and_name_fields_in_bosnian()
    {
        BosnianValidation.Configure();
        var result = new SampleValidator().Validate(new Sample("", "ab", "nije-email", 9, 0m, "x1", "predugo", null));

        result.Errors.Should().HaveCount(8);
        foreach (var e in result.Errors)
            foreach (var word in English)
                e.ErrorMessage.Should().NotContainEquivalentOf(word, $"'{e.ErrorMessage}' must not be English");
        result.Errors.Single(e => e.PropertyName == "Name").ErrorMessage.Should().Contain("Naziv");
        result.Errors.Single(e => e.PropertyName == "Reason").ErrorMessage.Should().Contain("Razlog");
        result.Errors.Single(e => e.PropertyName == "Quantity").ErrorMessage.Should().Contain("Količina");
    }

    [Fact]
    public void An_unknown_property_keeps_the_default_name_and_explicit_names_win()
    {
        BosnianValidation.NameOf("Reason").Should().Be("Razlog");
        BosnianValidation.NameOf("SomethingElse").Should().BeNull();
        BosnianValidation.NameOf(null).Should().BeNull();
    }
}
