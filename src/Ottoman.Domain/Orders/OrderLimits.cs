namespace Ottoman.Domain.Orders;

/// <summary>
/// Pure anti-hoarding rules. An order that holds stock without being paid for (a card order awaiting payment or a
/// pay-at-pickup order not yet handed over) takes goods off the shelf for free, so one account may only hold a little.
/// </summary>
public static class OrderLimits
{
    public const int MaxUnpaidOrdersPerClient = 5;
    public const int MaxUnpaidUnitsPerClient = 50;

    /// <summary>Whether the order holds stock that nobody has paid for yet.</summary>
    public static bool IsUnpaidHold(OrderStatus status, PaymentMethod method) =>
        status == OrderStatus.PendingPayment || (status == OrderStatus.Placed && method == PaymentMethod.OnReceipt);

    /// <summary>
    /// Whether a client who already holds <paramref name="unpaidOrders"/> unpaid orders with <paramref name="unpaidUnits"/>
    /// pieces in total may open another one of <paramref name="newUnits"/> pieces. Returns error text or null.
    /// </summary>
    public static string? CheckNewOrder(int unpaidOrders, int unpaidUnits, int newUnits)
    {
        if (unpaidOrders >= MaxUnpaidOrdersPerClient)
            return "Imate previše otvorenih neplaćenih narudžbi. Platite, preuzmite ili otkažite neku od njih.";
        if (unpaidUnits + newUnits > MaxUnpaidUnitsPerClient)
            return $"Neplaćene narudžbe mogu zajedno imati najviše {MaxUnpaidUnitsPerClient} komada. Platite, preuzmite ili otkažite neku od njih.";
        return null;
    }
}
