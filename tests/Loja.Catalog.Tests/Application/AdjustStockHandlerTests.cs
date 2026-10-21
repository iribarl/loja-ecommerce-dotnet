using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.AdjustStock;
using Loja.Catalog.Domain;
using Loja.SharedKernel;

namespace Loja.Catalog.Tests.Application;

public sealed class AdjustStockHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly FakeAdjustments _adjustments = new();
    private readonly FakeCache _cache = new();

    private AdjustStockHandler Handler => new(_adjustments, _cache);

    [Fact]
    public async Task Valid_AppliesIt_AndDropsTheCache()
    {
        var result = await Handler.HandleAsync(Guid.NewGuid(), 5, "Reposição", Guid.NewGuid(), Ct);

        result.Value.ShouldBe(15);
        _adjustments.Calls.ShouldBe(1);
        _cache.Invalidations.ShouldBe(1);
    }

    [Fact]
    public async Task Invalid_NeverReachesTheDatabase()
    {
        var result = await Handler.HandleAsync(Guid.NewGuid(), 0, "Reposição", Guid.NewGuid(), Ct);

        result.Error.ShouldBe(CatalogErrors.ZeroAdjustment);
        _adjustments.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Refused_KeepsTheCache()
    {
        _adjustments.Next = CatalogErrors.StockWouldBeNegative(1);

        var result = await Handler.HandleAsync(Guid.NewGuid(), -5, "Avaria", Guid.NewGuid(), Ct);

        result.IsFailure.ShouldBeTrue();
        _cache.Invalidations.ShouldBe(0);
    }

    private sealed class FakeAdjustments : IStockAdjustments
    {
        public int Calls { get; private set; }

        public Result<int> Next { get; set; } = 15;

        public Task<Result<int>> AdjustAsync(Guid variantId, int quantity, string reason, Guid adjustedBy, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Next);
        }
    }
}
