using Loja.Catalog.Contracts;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Domain.Stock;
using Loja.Catalog.Infrastructure.Contracts;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Loja.IntegrationTests.Catalog;

public sealed class StockReservationsTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset InAnHour = DateTimeOffset.UtcNow.AddHours(1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reserve_TakesTheUnitsOutOfStock()
    {
        var variant = await NewVariant(stock: 5);

        var result = await Reserve(Guid.NewGuid(), (variant, 2));

        result.Succeeded.ShouldBeTrue();
        (await StockOf(variant)).ShouldBe(3);
    }

    [Fact]
    public async Task Reserve_IsAllOrNothing()
    {
        var plenty = await NewVariant(stock: 10);
        var scarce = await NewVariant(stock: 1);

        var result = await Reserve(Guid.NewGuid(), (plenty, 2), (scarce, 2));

        result.Succeeded.ShouldBeFalse();
        result.UnavailableVariantIds.ShouldBe([scarce]);
        (await StockOf(plenty)).ShouldBe(10); // the units taken for the first item were given back
        (await StockOf(scarce)).ShouldBe(1);
    }

    [Fact]
    public async Task Reserve_UnpublishedProduct_IsUnavailable()
    {
        var variant = await NewVariant(stock: 5, published: false);

        (await Reserve(Guid.NewGuid(), (variant, 1))).Succeeded.ShouldBeFalse();
        (await StockOf(variant)).ShouldBe(5);
    }

    [Fact]
    public async Task Reserve_SameOrderTwice_ReservesOnce()
    {
        var variant = await NewVariant(stock: 5);
        var order = Guid.NewGuid();

        await Reserve(order, (variant, 2));
        (await Reserve(order, (variant, 2))).Succeeded.ShouldBeTrue();

        (await StockOf(variant)).ShouldBe(3);
    }

    [Fact]
    public async Task TenCustomersForTheLastThreeUnits_ExactlyThreeGetOne()
    {
        var variant = await NewVariant(stock: 3);

        // Ten checkouts at the same moment, each with its own connection, as in production.
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Reserve(Guid.NewGuid(), (variant, 1))));

        results.Count(r => r.Succeeded).ShouldBe(3);
        (await StockOf(variant)).ShouldBe(0);
    }

    [Fact]
    public async Task Release_ReturnsTheUnits_OnlyOnce()
    {
        var variant = await NewVariant(stock: 5);
        var order = Guid.NewGuid();
        await Reserve(order, (variant, 2));

        // The same release twice at the same time, like an event delivered twice.
        await Task.WhenAll(Release(order), Release(order));
        await Release(order);

        (await StockOf(variant)).ShouldBe(5);
        (await ReservationsOf(order)).ShouldAllBe(r => r.Status == ReservationStatus.Released);
    }

    [Fact]
    public async Task ConfirmedReservation_IsNotReleasedAnymore()
    {
        var variant = await NewVariant(stock: 5);
        var order = Guid.NewGuid();
        await Reserve(order, (variant, 2));

        await using (var db = postgres.CreateScratchCatalogDbContext())
        {
            await new StockReservations(db, TimeProvider.System).ConfirmAsync(order, Ct);
        }

        await Release(order);

        (await StockOf(variant)).ShouldBe(3);
        (await ReservationsOf(order)).ShouldAllBe(r => r.Status == ReservationStatus.Confirmed);
    }

    [Fact]
    public async Task ExpiredReservations_GoBackToStock()
    {
        var variant = await NewVariant(stock: 5);
        var order = Guid.NewGuid();
        await using (var db = postgres.CreateScratchCatalogDbContext())
        {
            await new StockReservations(db, TimeProvider.System)
                .ReserveAsync(order, [new StockRequest(variant, 2)], DateTimeOffset.UtcNow.AddMinutes(-1), Ct);
        }

        await using (var db = postgres.CreateScratchCatalogDbContext())
        {
            (await new StockReservations(db, TimeProvider.System).ReleaseExpiredAsync(Ct)).ShouldBeGreaterThanOrEqualTo(1);
        }

        (await StockOf(variant)).ShouldBe(5);
    }

    private async Task<ReservationResult> Reserve(Guid order, params (Guid Variant, int Quantity)[] items)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        return await new StockReservations(db, TimeProvider.System)
            .ReserveAsync(order, items.Select(i => new StockRequest(i.Variant, i.Quantity)).ToList(), InAnHour, Ct);
    }

    private async Task Release(Guid order)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        await new StockReservations(db, TimeProvider.System).ReleaseAsync(order, Ct);
    }

    private async Task<int> StockOf(Guid variant)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        var id = new ProductVariantId(variant);
        return await db.Set<ProductVariant>().Where(v => v.Id == id).Select(v => v.Stock).SingleAsync(Ct);
    }

    private async Task<List<StockReservation>> ReservationsOf(Guid order)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        return await db.StockReservations.AsNoTracking().Where(r => r.OrderId == order).ToListAsync(Ct);
    }

    // In the scratch database: tests that count the seed's products must not see these.
    private async Task<Guid> NewVariant(int stock, bool published = true)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        var category = Category.Create($"Categoria {Guid.NewGuid():N}").Value;
        db.Categories.Add(category);
        var product = Product.Create($"Produto de teste {Guid.NewGuid():N}", "", category.Id).Value;
        var sku = Sku.Create($"TST-{Guid.NewGuid():N}"[..20]).Value;
        var variant = product.AddVariant(sku, VariantAttributes.None, Money.Of(10m), stock).Value;
        if (published)
        {
            product.Publish();
        }

        db.Products.Add(product);
        await db.SaveChangesAsync(Ct);
        return variant.Id.Value;
    }
}
