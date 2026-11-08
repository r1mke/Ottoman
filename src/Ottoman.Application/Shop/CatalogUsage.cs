namespace Ottoman.Application.Shop;

/// <summary>Why a brand or a product category cannot be deleted yet: it is still used by products or shop sections.</summary>
public static class CatalogUsage
{
    /// <summary>
    /// The message for a refused delete. <paramref name="products"/> counts products of any state (deleting would strip them),
    /// <paramref name="sections"/> the shop sections that filter by it or show it as a tile.
    /// </summary>
    public static string InUseMessage(bool isBrand, int products, int sections)
    {
        var what = isBrand ? "Brend" : "Kategorija";
        var fix = isBrand ? "Prvo im promijenite brend" : "Prvo im promijenite kategoriju";
        return (products, sections) switch
        {
            ( > 0, > 0) => $"{what} se koristi kod {products} proizvoda i u sekcijama trgovine ({sections}). {fix} i uredite sekcije.",
            ( > 0, _) => $"{what} se koristi kod {products} proizvoda. {fix}.",
            (_, > 0) => $"{what} se koristi u sekcijama trgovine ({sections}). Prvo uredite ili obrišite te sekcije.",
            // Usage vanished between the refused delete and the count: the admin can simply try again.
            _ => $"{what} se upravo koristi. Pokušajte ponovo.",
        };
    }
}
