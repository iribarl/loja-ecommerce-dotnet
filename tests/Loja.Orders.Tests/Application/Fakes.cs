using Loja.Cart.Contracts;
using Loja.Catalog.Contracts;
using Loja.Customers.Contracts;
using Loja.Orders.Application;
using Loja.Orders.Domain;
using Loja.Shipping.Contracts;

namespace Loja.Orders.Tests.Application;

internal sealed class FakeCustomers : ICustomerDirectory
{
    public Dictionary<Guid, CustomerInfo> Customers { get; } = [];

    public Task<CustomerInfo?> GetAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult(Customers.GetValueOrDefault(customerId));
}

internal sealed class FakeCart : ICartApi
{
    public Dictionary<Guid, List<CartLine>> Carts { get; } = [];

    public bool FailToClear { get; set; }

    public Task<IReadOnlyList<CartLine>> GetCustomerCartAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CartLine>>(Carts.GetValueOrDefault(customerId) ?? []);

    public Task ClearCustomerCartAsync(Guid customerId, CancellationToken cancellationToken)
    {
        if (FailToClear)
        {
            throw new InvalidOperationException("Cart is down.");
        }

        Carts.Remove(customerId);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCatalog : ICatalogApi
{
    public Dictionary<Guid, VariantSnapshot> Variants { get; } = [];

    public VariantSnapshot Add(decimal price, int stock = 10, bool published = true)
    {
        var variant = new VariantSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Camiseta", "camiseta", $"SKU-{Variants.Count}",
            new Dictionary<string, string> { ["Tamanho"] = "M" }, price, "BRL", null, stock, published);
        Variants[variant.VariantId] = variant;
        return variant;
    }

    public Task<IReadOnlyList<VariantSnapshot>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<VariantSnapshot>>(variantIds.Where(Variants.ContainsKey).Select(id => Variants[id]).ToList());
}

internal sealed class FakeShipping : IShippingApi
{
    public Task<ShippingQuote?> QuoteAsync(string cep, decimal subtotal, string currency, CancellationToken cancellationToken) =>
        Task.FromResult<ShippingQuote?>(new ShippingQuote(cep, "Sudeste e Sul", 19.90m, currency, false, 3, 5, 299m));
}

internal sealed class FakeStock : IStockReservations
{
    public List<(Guid OrderId, IReadOnlyCollection<StockRequest> Items)> Reserved { get; } = [];

    public List<Guid> Released { get; } = [];

    public bool Fail { get; set; }

    public Task<ReservationResult> ReserveAsync(Guid orderId, IReadOnlyCollection<StockRequest> items, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            return Task.FromResult(new ReservationResult(false, [items.First().VariantId]));
        }

        Reserved.Add((orderId, items));
        return Task.FromResult(ReservationResult.Success);
    }

    public Task ReleaseAsync(Guid orderId, CancellationToken cancellationToken)
    {
        Released.Add(orderId);
        return Task.CompletedTask;
    }

    public Task ConfirmAsync(Guid orderId, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class InMemoryOrders : IOrderRepository
{
    public List<Order> Orders { get; } = [];

    public bool FailToSave { get; set; }

    private readonly List<Order> _pending = [];

    public Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.Find(o => o.Id == id));

    public Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Order>>(Orders.Where(o => o.Customer.CustomerId == customerId).OrderByDescending(o => o.PlacedAt).ToList());

    public void Add(Order order) => _pending.Add(order);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (FailToSave)
        {
            _pending.Clear();
            throw new InvalidOperationException("Database is down.");
        }

        Orders.AddRange(_pending);
        _pending.Clear();
        return Task.CompletedTask;
    }
}
