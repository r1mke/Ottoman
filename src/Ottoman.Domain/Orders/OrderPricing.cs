namespace Ottoman.Domain.Orders;

public readonly record struct OrderLine(int ProductId, decimal UnitPrice, int Quantity)
{
    public decimal Total => UnitPrice * Quantity;
}

/// <summary>Pure money rules for the shop. All amounts are salon currency (BAM) unless a method says otherwise.</summary>
public static class OrderPricing
{
    public const int MaxQuantityPerLine = 20;
    public const int MaxLines = 30;

    /// <summary>Fixed peg of the convertible mark to the euro.</summary>
    public const decimal DefaultBamPerEur = 1.95583m;

    public static decimal Total(IEnumerable<OrderLine> lines) => lines.Sum(l => l.Total);

    /// <summary>
    /// Smallest currency unit (cents) the card processor charges. Rounds half away from zero so the
    /// customer is never charged less than the converted price.
    /// </summary>
    public static long ToMinorUnits(decimal amountBam, decimal bamPerEur)
    {
        if (bamPerEur <= 0) throw new ArgumentOutOfRangeException(nameof(bamPerEur));
        return (long)Math.Round(amountBam / bamPerEur * 100m, MidpointRounding.AwayFromZero);
    }

    /// <summary>Merges duplicate products into one line and validates quantities. Returns error text or null.</summary>
    public static string? Validate(IReadOnlyList<(int ProductId, int Quantity)> requested)
    {
        if (requested.Count == 0) return "Košarica je prazna.";
        if (requested.Select(r => r.ProductId).Distinct().Count() > MaxLines) return $"Najviše {MaxLines} različitih proizvoda po narudžbi.";
        if (requested.Any(r => r.Quantity < 1)) return "Količina mora biti barem 1.";
        var merged = Merge(requested);
        if (merged.Any(m => m.Quantity > MaxQuantityPerLine)) return $"Najviše {MaxQuantityPerLine} komada istog proizvoda.";
        return null;
    }

    public static IReadOnlyList<(int ProductId, int Quantity)> Merge(IEnumerable<(int ProductId, int Quantity)> requested) =>
        requested.GroupBy(r => r.ProductId)
            .Select(g => (g.Key, g.Sum(x => x.Quantity)))
            .OrderBy(x => x.Key)
            .ToList();
}
