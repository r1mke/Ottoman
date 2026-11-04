using FluentAssertions;
using Ottoman.Domain.Orders;

namespace Ottoman.Domain.Tests;

public class OrderPaidTests
{
    [Theory]
    [InlineData(OrderStatus.Placed, PaymentMethod.Card, PaymentStatus.Succeeded, true)]
    [InlineData(OrderStatus.Fulfilled, PaymentMethod.Card, PaymentStatus.Succeeded, true)]
    [InlineData(OrderStatus.PendingPayment, PaymentMethod.Card, PaymentStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, PaymentMethod.Card, PaymentStatus.Refunded, false)]
    [InlineData(OrderStatus.Expired, PaymentMethod.Card, PaymentStatus.RefundPending, false)]
    [InlineData(OrderStatus.Placed, PaymentMethod.OnReceipt, null, false)]
    [InlineData(OrderStatus.Fulfilled, PaymentMethod.OnReceipt, null, true)]
    public void An_order_is_paid_when_the_card_payment_went_through_or_it_was_paid_at_pickup(
        OrderStatus status, PaymentMethod method, PaymentStatus? payment, bool paid) =>
        OrderStateMachine.IsPaid(status, method, payment).Should().Be(paid);
}
