using Loja.Orders.Application;
using Loja.Orders.Application.CancelOrder;
using Loja.Orders.Application.GetOrders;
using Loja.Orders.Application.PlaceOrder;
using Loja.Orders.Contracts;
using Loja.Orders.Infrastructure.Consumers;
using Loja.Payments.Contracts;
using Loja.SharedKernel;
using Loja.Orders.Infrastructure.Persistence;
using Loja.Outbox;
using Loja.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loja.Orders.Infrastructure;

/// <summary>
/// Entry point of the Orders module.
/// </summary>
/// <remarks>Needs the contracts of Catalog, Cart, Customers and Shipping, registered by those modules.</remarks>
public static class OrdersModule
{
    // Stateless, so one instance serves every pooled DbContext.
    private static readonly OutboxInterceptor Outbox = new(new OrdersIntegrationEventMapper());

    public static IHostApplicationBuilder AddOrdersModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<OrdersDbContext>(options => options
            .UseNpgsql(builder.Configuration.GetConnectionString(LojaDatabase.ConnectionStringName), OrdersDbContext.ConfigureNpgsql)
            .AddInterceptors(Outbox));
        builder.EnrichNpgsqlDbContext<OrdersDbContext>();

        // Delivers OrderPlaced, OrderCancelled... to the other modules' consumers.
        builder.Services.AddOutboxProcessor<OrdersDbContext>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<IOrdersApi, OrdersApi>();

        // Integration events from the Payments module.
        builder.Services.AddScoped<IIntegrationEventConsumer<PixChargeCreatedIntegrationEvent>, MarkOrderAwaitingPayment>();

        builder.Services.AddScoped<PlaceOrderHandler>();
        builder.Services.AddScoped<CancelOrderHandler>();
        builder.Services.AddScoped<GetMyOrdersHandler>();
        builder.Services.AddScoped<GetMyOrderHandler>();

        return builder;
    }

    /// <summary>Applies pending migrations. Development only.</summary>
    public static async Task InitializeOrdersDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(OrdersModule));

        await db.MigrateWithRetryAsync(logger, cancellationToken);
    }
}
