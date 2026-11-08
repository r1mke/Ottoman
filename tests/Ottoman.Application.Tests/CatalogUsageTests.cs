using FluentAssertions;
using Ottoman.Application.Shop;

namespace Ottoman.Application.Tests;

public class CatalogUsageTests
{
    [Fact]
    public void Products_only_names_the_count()
    {
        CatalogUsage.InUseMessage(isBrand: true, products: 3, sections: 0)
            .Should().Be("Brend se koristi kod 3 proizvoda. Prvo im promijenite brend.");
        CatalogUsage.InUseMessage(isBrand: false, products: 1, sections: 0)
            .Should().Be("Kategorija se koristi kod 1 proizvoda. Prvo im promijenite kategoriju.");
    }

    [Fact]
    public void Sections_only_and_both_are_described()
    {
        CatalogUsage.InUseMessage(isBrand: false, products: 0, sections: 2)
            .Should().StartWith("Kategorija se koristi u sekcijama trgovine (2).");
        CatalogUsage.InUseMessage(isBrand: true, products: 4, sections: 1)
            .Should().StartWith("Brend se koristi kod 4 proizvoda i u sekcijama trgovine (1).");
    }

    [Fact]
    public void Usage_that_vanished_asks_to_try_again()
    {
        CatalogUsage.InUseMessage(isBrand: true, products: 0, sections: 0).Should().Contain("Pokušajte ponovo");
    }
}
