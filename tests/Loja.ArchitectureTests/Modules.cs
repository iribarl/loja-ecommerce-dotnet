using System.Reflection;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace Loja.ArchitectureTests;

/// <summary>
/// The assemblies of the solution, grouped by module. Add new modules and layers here.
/// </summary>
internal static class Modules
{
    public static readonly string[] Names = ["Catalog", "Cart", "Customers", "Shipping", "Orders", "Payments"];

    public static readonly string[] InternalLayers = ["Domain", "Application", "Infrastructure"];

    /// <summary>Every internal (non-Contracts) assembly of every module.</summary>
    public static readonly (string Module, Assembly Assembly)[] InternalAssemblies =
    [
        ("Catalog", typeof(Catalog.Domain.Products.Product).Assembly),
        ("Catalog", typeof(Catalog.Application.Products.IProductQueries).Assembly),
        ("Catalog", typeof(Catalog.Infrastructure.CatalogModule).Assembly),
        ("Cart", typeof(Cart.Domain.ShoppingCart).Assembly),
        ("Cart", typeof(Cart.Application.ICartRepository).Assembly),
        ("Cart", typeof(Cart.Infrastructure.CartModule).Assembly),
        ("Customers", typeof(Customers.Infrastructure.CustomersModule).Assembly),
        ("Shipping", typeof(Shipping.Domain.ShippingRates).Assembly),
        ("Shipping", typeof(Shipping.Infrastructure.ShippingModule).Assembly),
        ("Orders", typeof(Orders.Domain.Order).Assembly),
        ("Orders", typeof(Orders.Application.IOrderRepository).Assembly),
        ("Orders", typeof(Orders.Infrastructure.OrdersModule).Assembly),
        ("Payments", typeof(Payments.Domain.Payment).Assembly),
        ("Payments", typeof(Payments.Application.IPaymentGateway).Assembly),
        ("Payments", typeof(Payments.Infrastructure.PaymentsModule).Assembly),
    ];

    public static readonly (string Module, Assembly Assembly)[] ContractAssemblies =
    [
        ("Catalog", typeof(Catalog.Contracts.ICatalogApi).Assembly),
        ("Shipping", typeof(Shipping.Contracts.IShippingApi).Assembly),
        ("Cart", typeof(Cart.Contracts.ICartApi).Assembly),
        ("Customers", typeof(Customers.Contracts.ICustomerDirectory).Assembly),
        ("Orders", typeof(Orders.Contracts.OrderPlacedIntegrationEvent).Assembly),
        ("Payments", typeof(Payments.Contracts.PaymentConfirmedIntegrationEvent).Assembly),
    ];

    /// <summary>Namespaces of the internal layers of a module, e.g. Loja.Cart.Domain.</summary>
    public static string[] InternalNamespacesOf(string module) =>
        InternalLayers.Select(layer => $"Loja.{module}.{layer}").ToArray();

    public static string Describe(TestResult result) =>
        $"Types with forbidden dependencies: {string.Join(", ", result.FailingTypeNames ?? [])}";

    public static TestResult CheckNoDependency(Assembly assembly, params string[] namespaces) =>
        Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(namespaces).GetResult();
}
