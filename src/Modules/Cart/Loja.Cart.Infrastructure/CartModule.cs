using Loja.Cart.Application;
using Loja.Cart.Application.AddToCart;
using Loja.Cart.Application.GetCart;
using Loja.Cart.Application.GetCartUnits;
using Loja.Cart.Application.UpdateCartItem;
using Loja.Cart.Infrastructure.Persistence;
using Loja.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loja.Cart.Infrastructure;

/// <summary>
/// Entry point of the Cart module for the host.
/// </summary>
/// <remarks>Needs an <c>ICatalogApi</c>, registered by the Catalog module.</remarks>
public static class CartModule
{
    public static IHostApplicationBuilder AddCartModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<CartDbContext>(options => options
            .UseNpgsql(builder.Configuration.GetConnectionString(LojaDatabase.ConnectionStringName), CartDbContext.ConfigureNpgsql));
        builder.EnrichNpgsqlDbContext<CartDbContext>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ICartRepository, CartRepository>();

        builder.Services.AddScoped<AddToCartHandler>();
        builder.Services.AddScoped<UpdateCartItemHandler>();
        builder.Services.AddScoped<GetCartHandler>();
        builder.Services.AddScoped<GetCartUnitsHandler>();

        return builder;
    }

    /// <summary>Applies pending migrations. Development only.</summary>
    public static async Task InitializeCartDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CartDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CartModule));

        await db.MigrateWithRetryAsync(logger, cancellationToken);
    }
}
