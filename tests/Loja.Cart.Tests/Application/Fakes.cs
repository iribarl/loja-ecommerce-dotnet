using Loja.Cart.Application;
using Loja.Cart.Domain;
using Loja.Catalog.Contracts;

namespace Loja.Cart.Tests.Application;

/// <summary>Catalog with the variants the test puts in it.</summary>
internal sealed class FakeCatalogApi : ICatalogApi
{
    private readonly Dictionary<Guid, VariantSnapshot> _variants = [];

    public VariantSnapshot Add(decimal price = 59.90m, int stock = 10, bool published = true)
    {
        var id = Guid.NewGuid();
        var variant = new VariantSnapshot(
            id, Guid.NewGuid(), "Camiseta", "camiseta", $"SKU-{_variants.Count + 1}",
            new Dictionary<string, string> { ["Tamanho"] = "M" }, price, "BRL", null, stock, published);
        _variants[id] = variant;
        return variant;
    }

    public void SetStock(Guid variantId, int stock) =>
        _variants[variantId] = _variants[variantId] with { AvailableStock = stock };

    public void Remove(Guid variantId) => _variants.Remove(variantId);

    public Task<IReadOnlyList<VariantSnapshot>> GetVariantsAsync(
        IReadOnlyCollection<Guid> variantIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<VariantSnapshot>>(
            variantIds.Where(_variants.ContainsKey).Select(id => _variants[id]).ToList());
}

/// <summary>Carts kept in memory; counts saves.</summary>
internal sealed class InMemoryCartRepository : ICartRepository
{
    public List<ShoppingCart> Carts { get; } = [];

    public int Saves { get; private set; }

    public Task<ShoppingCart?> GetByIdAsync(CartId id, CancellationToken cancellationToken) =>
        Task.FromResult(Carts.Find(c => c.Id == id));

    public Task<ShoppingCart?> GetByCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(Carts.Find(c => c.CustomerId == customerId));

    public void Add(ShoppingCart cart) => Carts.Add(cart);

    public void Remove(ShoppingCart cart) => Carts.Remove(cart);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
