using Loja.Cart.Contracts;
using Loja.Catalog.Contracts;
using Loja.Customers.Contracts;
using Loja.Orders.Domain;
using Loja.SharedKernel;
using Loja.Shipping.Contracts;

namespace Loja.Orders.Application.PlaceOrder;

/// <param name="CustomerId">The signed-in customer: checkout requires an account.</param>
public sealed record PlaceOrderRequest(
    Guid CustomerId,
    string Recipient,
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string District,
    string City,
    string State);

/// <summary>
/// Turns the customer's cart into an order. Coordinates several modules through their contracts;
/// there is no transaction across them, so the order of the steps matters (see below).
/// </summary>
public sealed class PlaceOrderHandler(
    ICustomerDirectory customers,
    ICartApi cart,
    ICatalogApi catalog,
    IShippingApi shipping,
    IStockReservations stock,
    IOrderRepository orders,
    TimeProvider clock)
{
    /// <summary>
    /// Safety net for reservations of orders that were never saved. Longer than any payment window:
    /// unpaid orders are cancelled (and their stock released) by the order expiry in phase 4.
    /// </summary>
    public static readonly TimeSpan ReservationSafetyWindow = TimeSpan.FromDays(2);

    /// <returns>The id of the new order (a plain Guid: callers must not depend on domain types).</returns>
    public async Task<Result<Guid>> HandleAsync(PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        var customer = await customers.GetAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return PlaceOrderErrors.CustomerNotFound;
        }

        var items = await cart.GetCustomerCartAsync(request.CustomerId, cancellationToken);
        if (items.Count == 0)
        {
            return PlaceOrderErrors.EmptyCart;
        }

        // Current prices from the catalog: they are frozen in the order from here on.
        var variants = (await catalog.GetVariantsAsync(items.Select(i => i.VariantId).ToList(), cancellationToken))
            .ToDictionary(v => v.VariantId);
        if (items.Any(item => !variants.TryGetValue(item.VariantId, out var v) || !v.CanBePurchased || v.AvailableStock < item.Quantity))
        {
            return PlaceOrderErrors.CartChanged;
        }

        // Everything that depends on what the customer typed is checked before taking any stock.
        var address = ShippingAddress.Create(
            request.Recipient, request.Cep, request.Street, request.Number, request.Complement,
            request.District, request.City, request.State);
        if (address.IsFailure)
        {
            return address.Error;
        }

        var lines = items
            .Select(item => ToLine(variants[item.VariantId], item.Quantity))
            .ToList();
        var subtotal = lines.Aggregate(Money.Zero(lines[0].UnitPrice.Currency), (sum, line) => sum + line.LineTotal);

        var quote = await shipping.QuoteAsync(address.Value.Cep.Digits, subtotal.Amount, subtotal.Currency, cancellationToken);
        if (quote is null)
        {
            return Cep.Invalid;
        }

        var now = clock.GetUtcNow();
        var orderId = OrderId.New();
        var order = Order.Place(
            orderId,
            new CustomerSnapshot(customer.CustomerId, customer.FullName, customer.Email),
            lines,
            address.Value,
            new ShippingOption(quote.Region, Money.Of(quote.Price, quote.Currency), quote.MinDays, quote.MaxDays),
            now);
        if (order.IsFailure)
        {
            return order.Error;
        }

        // Atomic in the Catalog: if any unit is missing (someone bought it a moment ago), nothing is taken.
        var reservation = await stock.ReserveAsync(
            orderId.Value,
            items.Select(item => new StockRequest(item.VariantId, item.Quantity)).ToList(),
            now + ReservationSafetyWindow,
            cancellationToken);
        if (!reservation.Succeeded)
        {
            return PlaceOrderErrors.OutOfStock;
        }

        try
        {
            orders.Add(order.Value);
            await orders.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Compensation: no order, so the stock must go back. (If even this fails, the
            // reservation expires and the sweeper returns the units.)
            await stock.ReleaseAsync(orderId.Value, CancellationToken.None);
            throw;
        }

        // The order exists now: a failure to empty the cart must not undo it or show an error.
#pragma warning disable CA1031 // Any failure here is acceptable: the cart simply stays filled.
        try
        {
            await cart.ClearCustomerCartAsync(request.CustomerId, cancellationToken);
        }
        catch (Exception)
        {
        }
#pragma warning restore CA1031

        return orderId.Value;
    }

    private static OrderLine ToLine(VariantSnapshot variant, int quantity) =>
        new(variant.VariantId, variant.ProductId, variant.Sku, variant.ProductName, variant.Attributes,
            Money.Of(variant.Price, variant.Currency), quantity);
}

public static class PlaceOrderErrors
{
    public static readonly Error CustomerNotFound =
        Error.NotFound("Orders.CustomerNotFound", "Cliente não encontrado. Entre novamente.");

    public static readonly Error EmptyCart =
        Error.Validation("Orders.EmptyCart", "Seu carrinho está vazio.");

    public static readonly Error CartChanged =
        Error.Conflict("Orders.CartChanged", "Alguns itens do carrinho mudaram de preço ou de estoque. Revise o carrinho.");

    public static readonly Error OutOfStock =
        Error.Conflict("Orders.OutOfStock", "Um item acabou de esgotar. Revise o carrinho e tente novamente.");
}
