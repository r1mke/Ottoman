using FluentAssertions;
using Ottoman.Domain;
using Ottoman.Domain.Orders;
using Ottoman.Domain.Reservations;

namespace Ottoman.Domain.Tests;

public class OrderPricingTests
{
    [Fact]
    public void Total_is_the_sum_of_unit_price_times_quantity()
    {
        OrderPricing.Total([new(1, 10m, 2), new(2, 12.50m, 3)]).Should().Be(57.50m);
    }

    [Theory]
    [InlineData(19.5583, 1000)]   // exactly 10 EUR
    [InlineData(10, 511)]         // 5.1129 EUR -> 5.11 would under-charge; 511.29 cents rounds to 511
    [InlineData(0.01, 1)]         // never rounds a real price down to nothing
    public void BAM_converts_to_euro_cents_at_the_fixed_peg(double bam, long cents)
    {
        OrderPricing.ToMinorUnits((decimal)bam, OrderPricing.DefaultBamPerEur).Should().Be(cents);
    }

    [Fact]
    public void Rounding_is_half_away_from_zero()
    {
        // 1.955830 BAM at peg 1.95583 = exactly 1 EUR; 0.977915 BAM = 0.5 EUR = 50 cents; just above half a cent rounds up
        OrderPricing.ToMinorUnits(0.977915m, 1.95583m).Should().Be(50);
        OrderPricing.ToMinorUnits(1m, 2m).Should().Be(50);
        OrderPricing.ToMinorUnits(0.01m, 2m).Should().Be(1); // 0.5 cent -> 1
    }

    [Fact]
    public void A_non_positive_rate_is_a_programming_error()
    {
        var act = () => OrderPricing.ToMinorUnits(10m, 0m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Duplicate_products_are_merged_into_one_line()
    {
        OrderPricing.Merge([(5, 1), (3, 2), (5, 2)]).Should().Equal((3, 2), (5, 3));
    }

    [Fact]
    public void Validation_rejects_empty_zero_negative_and_oversized_baskets()
    {
        OrderPricing.Validate([]).Should().NotBeNull();
        OrderPricing.Validate([(1, 0)]).Should().NotBeNull();
        OrderPricing.Validate([(1, -2)]).Should().NotBeNull();
        OrderPricing.Validate([(1, OrderPricing.MaxQuantityPerLine + 1)]).Should().NotBeNull();
        // the same product added twice counts together
        OrderPricing.Validate([(1, OrderPricing.MaxQuantityPerLine), (1, 1)]).Should().NotBeNull();
        OrderPricing.Validate([(1, 2), (2, 1)]).Should().BeNull();
    }
}

public class OrderStateMachineTests
{
    [Theory]
    [InlineData(OrderStatus.PendingPayment, ActorKind.Client, PaymentMethod.Card, true)]
    [InlineData(OrderStatus.PendingPayment, ActorKind.Admin, PaymentMethod.Card, true)]
    [InlineData(OrderStatus.Placed, ActorKind.Client, PaymentMethod.OnReceipt, true)]   // not paid yet: client may withdraw
    [InlineData(OrderStatus.Placed, ActorKind.Client, PaymentMethod.Card, false)]       // paid: refund is the salon's call
    [InlineData(OrderStatus.Placed, ActorKind.Admin, PaymentMethod.Card, true)]
    [InlineData(OrderStatus.Fulfilled, ActorKind.Admin, PaymentMethod.OnReceipt, false)]
    [InlineData(OrderStatus.Cancelled, ActorKind.Admin, PaymentMethod.Card, false)]
    [InlineData(OrderStatus.Expired, ActorKind.Client, PaymentMethod.Card, false)]
    public void Cancel_rules(OrderStatus status, ActorKind actor, PaymentMethod method, bool allowed)
    {
        OrderStateMachine.CanCancel(status, actor, method).Allowed.Should().Be(allowed);
    }

    [Theory]
    [InlineData(OrderStatus.Placed, ActorKind.Admin, true)]
    [InlineData(OrderStatus.Placed, ActorKind.Client, false)]
    [InlineData(OrderStatus.Placed, ActorKind.Employee, false)]
    [InlineData(OrderStatus.PendingPayment, ActorKind.Admin, false)] // cannot hand over goods that are not paid for
    [InlineData(OrderStatus.Fulfilled, ActorKind.Admin, false)]
    [InlineData(OrderStatus.Cancelled, ActorKind.Admin, false)]
    public void Fulfil_rules(OrderStatus status, ActorKind actor, bool allowed)
    {
        OrderStateMachine.CanFulfil(status, actor).Allowed.Should().Be(allowed);
    }

    [Theory]
    [InlineData(OrderStatus.PendingPayment, true)]
    [InlineData(OrderStatus.Placed, true)]
    [InlineData(OrderStatus.Fulfilled, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Expired, false)]
    public void Stock_is_held_only_while_the_order_is_open(OrderStatus status, bool holds)
    {
        OrderStateMachine.HoldsStock(status).Should().Be(holds);
    }

    [Fact]
    public void Only_a_pending_order_accepts_a_payment()
    {
        OrderStateMachine.AcceptsPayment(OrderStatus.PendingPayment).Should().BeTrue();
        OrderStateMachine.AcceptsPayment(OrderStatus.Expired).Should().BeFalse();
        OrderStateMachine.AcceptsPayment(OrderStatus.Cancelled).Should().BeFalse();
    }
}
