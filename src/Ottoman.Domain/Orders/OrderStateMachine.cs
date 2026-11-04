using Ottoman.Domain.Reservations;

namespace Ottoman.Domain.Orders;

/// <summary>Pure order lifecycle rules. Ownership is checked by the caller.</summary>
public static class OrderStateMachine
{
    public static Transition CanCancel(OrderStatus status, ActorKind actor, PaymentMethod method)
    {
        switch (status)
        {
            case OrderStatus.Fulfilled:
                return Transition.Deny("Narudžba je već preuzeta i ne može se otkazati.");
            case OrderStatus.Cancelled or OrderStatus.Expired:
                return Transition.Deny("Narudžba je već zatvorena.");
            case OrderStatus.PendingPayment:
                return Transition.Ok;
            default: // Placed
                // A client may withdraw an order that has not been paid for yet; a paid order needs the salon (refund decision).
                return actor == ActorKind.Admin || method == PaymentMethod.OnReceipt
                    ? Transition.Ok
                    : Transition.Deny("Plaćenu narudžbu može otkazati samo salon. Kontaktirajte nas.");
        }
    }

    public static Transition CanFulfil(OrderStatus status, ActorKind actor)
    {
        if (actor != ActorKind.Admin) return Transition.Deny("Samo administrator može zatvoriti narudžbu.");
        return status == OrderStatus.Placed
            ? Transition.Ok
            : Transition.Deny("Narudžba mora biti zaprimljena (plaćena ili za plaćanje pri preuzimanju).");
    }

    /// <summary>
    /// Whether the money for the order is in: a card order once its payment succeeded (and was not refunded),
    /// a pay-at-pickup order once it was handed over (paid at the counter).
    /// </summary>
    public static bool IsPaid(OrderStatus status, PaymentMethod method, PaymentStatus? payment) =>
        method == PaymentMethod.Card ? payment == PaymentStatus.Succeeded : status == OrderStatus.Fulfilled;

    /// <summary>Whether a successful card payment should move the order forward.</summary>
    public static bool AcceptsPayment(OrderStatus status) => status == OrderStatus.PendingPayment;

    /// <summary>Whether the order's stock is currently held (and must be returned when the order is closed unfulfilled).</summary>
    public static bool HoldsStock(OrderStatus status) => status is OrderStatus.PendingPayment or OrderStatus.Placed;
}
