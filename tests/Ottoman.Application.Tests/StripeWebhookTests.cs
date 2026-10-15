using FluentAssertions;
using FluentValidation;
using Ottoman.Application.Orders;
using Ottoman.Application.Payments;
using Ottoman.Application.Shop;
using Ottoman.Domain;

namespace Ottoman.Application.Tests;

public class StripeWebhookTests
{
    private const string Secret = "whsec_unit";
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private const string Body = """{"id":"evt_1","type":"payment_intent.succeeded","data":{"object":{"id":"pi_1","amount":1000,"amount_received":1000,"currency":"eur"}}}""";

    [Fact]
    public void A_correct_signature_is_accepted()
    {
        StripeWebhook.VerifySignature(Body, StripeWebhook.Header(Body, Secret, Now), Secret, Now).Should().BeTrue();
    }

    [Fact]
    public void A_tampered_body_is_rejected()
    {
        var header = StripeWebhook.Header(Body, Secret, Now);
        StripeWebhook.VerifySignature(Body.Replace("1000", "1"), header, Secret, Now).Should().BeFalse();
    }

    [Fact]
    public void A_signature_made_with_another_secret_is_rejected()
    {
        StripeWebhook.VerifySignature(Body, StripeWebhook.Header(Body, "whsec_other", Now), Secret, Now).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("t=abc,v1=00")]
    [InlineData("v1=deadbeef")]
    public void Missing_or_malformed_headers_are_rejected(string? header)
    {
        StripeWebhook.VerifySignature(Body, header, Secret, Now).Should().BeFalse();
    }

    [Fact]
    public void An_unconfigured_secret_rejects_everything()
    {
        StripeWebhook.VerifySignature(Body, StripeWebhook.Header(Body, "", Now), "", Now).Should().BeFalse();
    }

    [Fact]
    public void An_old_signature_is_a_replay_and_is_rejected()
    {
        var header = StripeWebhook.Header(Body, Secret, Now);
        var later = Now.AddSeconds(StripeWebhook.ToleranceSeconds + 1);
        StripeWebhook.VerifySignature(Body, header, Secret, later).Should().BeFalse();
        StripeWebhook.VerifySignature(Body, header, Secret, Now.AddSeconds(StripeWebhook.ToleranceSeconds - 1)).Should().BeTrue();
    }

    [Fact]
    public void Any_of_several_signatures_may_match_while_a_secret_is_rotated()
    {
        var good = StripeWebhook.Sign(Body, Now.ToUnixTimeSeconds(), Secret);
        var header = $"t={Now.ToUnixTimeSeconds()},v1={new string('0', 64)},v1={good}";
        StripeWebhook.VerifySignature(Body, header, Secret, Now).Should().BeTrue();
    }

    [Fact]
    public void A_succeeded_event_is_parsed()
    {
        var ev = StripeWebhook.Parse(Body)!;
        ev.Kind.Should().Be(PaymentEventKind.Succeeded);
        ev.ProviderRef.Should().Be("pi_1");
        ev.AmountMinor.Should().Be(1000);
        ev.Currency.Should().Be("eur");
        ev.EventId.Should().Be("evt_1");
    }

    [Fact]
    public void A_failed_event_is_parsed_and_other_types_are_ignored()
    {
        StripeWebhook.Parse("""{"id":"e","type":"payment_intent.payment_failed","data":{"object":{"id":"pi_2","amount":500,"currency":"EUR"}}}""")!
            .Should().Match<PaymentEvent>(e => e.Kind == PaymentEventKind.Failed && e.ProviderRef == "pi_2" && e.Currency == "eur");
        StripeWebhook.Parse("""{"id":"e","type":"charge.refunded","data":{"object":{}}}""")!.Kind.Should().Be(PaymentEventKind.Ignored);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"id":"e","type":"payment_intent.succeeded","data":{}}""")]
    public void Garbage_payloads_parse_to_null(string payload)
    {
        StripeWebhook.Parse(payload).Should().BeNull();
    }
}

public class ShopValidatorTests
{
    private static readonly CreateOrderRequestValidator Order = new();

    private static CreateOrderRequest Req(FulfilmentType f = FulfilmentType.Pickup, PaymentMethod m = PaymentMethod.Card, string? address = null, int qty = 1) =>
        new([new OrderLineRequest(1, qty)], f, m, address);

    [Fact]
    public void A_pickup_card_order_is_valid() => Order.Validate(Req()).IsValid.Should().BeTrue();

    [Fact]
    public void An_empty_basket_is_invalid() =>
        Order.Validate(new CreateOrderRequest([], FulfilmentType.Pickup, PaymentMethod.Card)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void Quantity_must_be_between_one_and_twenty(int qty) => Order.Validate(Req(qty: qty)).IsValid.Should().BeFalse();

    [Fact]
    public void Delivery_needs_an_address_and_a_card()
    {
        Order.Validate(Req(FulfilmentType.Delivery, PaymentMethod.Card)).IsValid.Should().BeFalse();
        Order.Validate(Req(FulfilmentType.Delivery, PaymentMethod.Card, "Ulica 1")).IsValid.Should().BeTrue();
        Order.Validate(Req(FulfilmentType.Delivery, PaymentMethod.OnReceipt, "Ulica 1")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Product_rules()
    {
        var v = new ProductUpsertRequestValidator();
        v.Validate(new ProductUpsertRequest("Vosak", null, null, null, 10m, 5, 2)).IsValid.Should().BeTrue();
        v.Validate(new ProductUpsertRequest("", null, null, null, 10m, 5, 2)).IsValid.Should().BeFalse();
        v.Validate(new ProductUpsertRequest("Vosak", null, null, null, 0m, 5, 2)).IsValid.Should().BeFalse();
        v.Validate(new ProductUpsertRequest("Vosak", null, null, null, 10.123m, 5, 2)).IsValid.Should().BeFalse();
        v.Validate(new ProductUpsertRequest("Vosak", null, null, null, 10m, -1, 2)).IsValid.Should().BeFalse();
        new StockAdjustRequestValidator().Validate(new StockAdjustRequest(0)).IsValid.Should().BeFalse();
        new StockAdjustRequestValidator().Validate(new StockAdjustRequest(-3)).IsValid.Should().BeTrue();
    }
}
