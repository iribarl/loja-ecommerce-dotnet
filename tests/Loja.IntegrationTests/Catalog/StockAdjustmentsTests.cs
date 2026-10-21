using Loja.Catalog.Contracts;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Loja.Catalog.Infrastructure.Contracts;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Loja.IntegrationTests.Catalog;

/// <summary>
/// Manual stock changes on a real Postgres, in the scratch database.
/// </summary>
public sealed class StockAdjustmentsTests(PostgresFixture postgres)
{
    private static readonly Guid Admin = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Adjust_ChangesTheStock_AndKeepsTheHistory()
    {
        var variant = await NewVariant(stock: 2);

        (await Adjust(variant, 10, "Reposição")).Value.ShouldBe(12);
        (await Adjust(variant, -3, "Avaria")).Value.ShouldBe(9);

        (await StockOf(variant)).ShouldBe(9);
        var history = await HistoryOf(variant);
        history.Select(a => (a.Quantity, a.Reason, a.StockAfter, a.AdjustedBy))
            .ShouldBe([(10, "Reposição", 12, Admin), (-3, "Avaria", 9, Admin)]);
    }

    [Fact]
    public async Task Adjust_BelowZero_IsRefused_AndChangesNothing()
    {
        var variant = await NewVariant(stock: 2);

        var result = await Adjust(variant, -3, "Contagem");

        result.Error.Code.ShouldBe("Catalog.StockWouldBeNegative");
        result.Error.Message.ShouldContain("há 2 unidade");
        (await StockOf(variant)).ShouldBe(2);
        (await HistoryOf(variant)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Adjust_UnknownVariant_IsNotFound()
    {
        (await Adjust(Guid.NewGuid(), 1, "Reposição")).Error.ShouldBe(CatalogErrors.VariantNotFound);
    }

    [Fact]
    public async Task ManyAdjustmentsAtOnce_NeverGoBelowZero()
    {
        var variant = await NewVariant(stock: 5);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Adjust(variant, -1, "Retirada")));

        results.Count(r => r.IsSuccess).ShouldBe(5);
        (await StockOf(variant)).ShouldBe(0);
        (await HistoryOf(variant)).Count.ShouldBe(5);
    }

    [Fact]
    public async Task AdjustmentAndPurchaseAtOnce_OnlyOneGetsTheLastUnits()
    {
        var variant = await NewVariant(stock: 5);

        var adjust = Adjust(variant, -3, "Avaria");
        var reserve = Reserve(variant, 3);
        await Task.WhenAll(adjust, reserve);

        ((await adjust).IsSuccess ^ (await reserve).Succeeded).ShouldBeTrue();
        (await StockOf(variant)).ShouldBe(2);
    }

    private async Task<Result<int>> Adjust(Guid variant, int quantity, string reason)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        return await new StockAdjustments(db, TimeProvider.System).AdjustAsync(variant, quantity, reason, Admin, Ct);
    }

    private async Task<ReservationResult> Reserve(Guid variant, int quantity)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        return await new StockReservations(db, TimeProvider.System)
            .ReserveAsync(Guid.NewGuid(), [new StockRequest(variant, quantity)], DateTimeOffset.UtcNow.AddHours(1), Ct);
    }

    private async Task<int> StockOf(Guid variant)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        var id = new ProductVariantId(variant);
        return await db.Set<ProductVariant>().Where(v => v.Id == id).Select(v => v.Stock).SingleAsync(Ct);
    }

    private async Task<List<Loja.Catalog.Domain.Stock.StockAdjustment>> HistoryOf(Guid variant)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        var id = new ProductVariantId(variant);
        return await db.StockAdjustments.AsNoTracking().Where(a => a.VariantId == id).OrderBy(a => a.AdjustedAt).ThenBy(a => a.Id).ToListAsync(Ct);
    }

    private async Task<Guid> NewVariant(int stock)
    {
        await using var db = postgres.CreateScratchCatalogDbContext();
        var category = Category.Create($"Categoria {Guid.NewGuid():N}").Value;
        db.Categories.Add(category);
        var product = Product.Create($"Produto de teste {Guid.NewGuid():N}", "", category.Id).Value;
        var variant = product.AddVariant(Sku.Create($"AJU-{Guid.NewGuid():N}"[..20]).Value, VariantAttributes.None, Money.Of(10m), stock).Value;
        product.Publish();
        db.Products.Add(product);
        await db.SaveChangesAsync(Ct);
        return variant.Id.Value;
    }
}
