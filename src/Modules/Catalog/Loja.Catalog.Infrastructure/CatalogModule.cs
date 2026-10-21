using Loja.Catalog.Application.Categories;
using Loja.Catalog.Application.Categories.ListCategories;
using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.AdjustStock;
using Loja.Catalog.Application.Products.GetFilterOptions;
using Loja.Catalog.Application.Products.Images;
using Loja.Catalog.Infrastructure.Images;
using Loja.Catalog.Application.Products.GetProductBySlug;
using Loja.Catalog.Application.Products.ManageProducts;
using Loja.Catalog.Application.Products.SearchProducts;
using Loja.Catalog.Contracts;
using Loja.Catalog.Infrastructure.Caching;
using Loja.Catalog.Infrastructure.Consumers;
using Loja.Orders.Contracts;
using Loja.SharedKernel;
using Loja.Catalog.Infrastructure.Contracts;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Catalog.Infrastructure.Persistence.Seed;
using Loja.Catalog.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Loja.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Loja.Catalog.Infrastructure;

/// <summary>
/// Entry point of the Catalog module for the host: one call registers everything the module needs.
/// </summary>
public static class CatalogModule
{
    /// <summary>URL path under which the host serves uploaded product images.</summary>
    public const string ImagesRequestPath = "/uploads/produtos";

    /// <summary>
    /// Folder of the uploaded product images: "Catalog:Images:Path" in configuration, otherwise
    /// "uploads/produtos" under the app's content root (ignored by git).
    /// </summary>
    public static string ImagesPath(IConfiguration configuration, IHostEnvironment environment) =>
        configuration["Catalog:Images:Path"] is { Length: > 0 } path
            ? path
            : Path.Combine(environment.ContentRootPath, "uploads", "produtos");

    public static IHostApplicationBuilder AddCatalogModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<CatalogDbContext>(options => options
            .UseNpgsql(builder.Configuration.GetConnectionString(LojaDatabase.ConnectionStringName), CatalogDatabase.ConfigureNpgsql));

        // Adds Aspire's retries, health check and telemetry to the context registered above.
        builder.EnrichNpgsqlDbContext<CatalogDbContext>();

        // Decorator pattern without extra libraries: the interface resolves to the cached version,
        // which wraps the real EF Core queries. Requires a HybridCache registered by the host.
        builder.Services.AddScoped<ProductQueries>();
        builder.Services.AddScoped<IProductQueries>(sp => new CachedProductQueries(
            sp.GetRequiredService<ProductQueries>(), sp.GetRequiredService<HybridCache>()));

        builder.Services.AddScoped<CategoryQueries>();
        builder.Services.AddScoped<ICategoryQueries>(sp => new CachedCategoryQueries(
            sp.GetRequiredService<CategoryQueries>(), sp.GetRequiredService<HybridCache>()));

        // Public contracts for other modules.
        builder.Services.AddScoped<ICatalogApi, CatalogApi>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<StockReservations>();
        builder.Services.AddScoped<IStockReservations>(sp => sp.GetRequiredService<StockReservations>());
        builder.Services.AddHostedService<ExpiredReservationsSweeper>();

        // Integration events from other modules.
        builder.Services.AddScoped<IIntegrationEventConsumer<OrderCancelledIntegrationEvent>, ReleaseStockWhenOrderCancelled>();
        builder.Services.AddScoped<IIntegrationEventConsumer<OrderPaidIntegrationEvent>, ConfirmStockWhenOrderPaid>();

        // Admin panel: writes through the repository, reads without cache.
        builder.Services.AddScoped<IProductRepository, ProductRepository>();
        builder.Services.AddScoped<IProductAdminQueries, ProductAdminQueries>();
        builder.Services.AddScoped<ICatalogCacheInvalidator, CatalogCacheInvalidator>();
        builder.Services.AddScoped<ListAdminProductsHandler>();
        builder.Services.AddScoped<GetAdminProductHandler>();
        builder.Services.AddScoped<CreateProductHandler>();
        builder.Services.AddScoped<UpdateProductHandler>();
        builder.Services.AddScoped<PublishProductHandler>();
        builder.Services.AddScoped<AddVariantHandler>();
        builder.Services.AddScoped<UpdateVariantHandler>();
        builder.Services.AddScoped<IStockAdjustments, StockAdjustments>();
        builder.Services.AddScoped<AdjustStockHandler>();
        builder.Services.AddSingleton<IImageStorage>(new LocalImageStorage(ImagesPath(builder.Configuration, builder.Environment)));
        builder.Services.AddScoped<AddProductImageHandler>();
        builder.Services.AddScoped<RemoveProductImageHandler>();

        builder.Services.AddScoped<SearchProductsHandler>();
        builder.Services.AddScoped<GetProductBySlugHandler>();
        builder.Services.AddScoped<ListCategoriesHandler>();
        builder.Services.AddScoped<GetFilterOptionsHandler>();

        return builder;
    }

    /// <summary>
    /// Applies pending migrations and seeds the demo data. Development only.
    /// </summary>
    public static async Task InitializeCatalogDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CatalogModule));

        await db.MigrateWithRetryAsync(logger, cancellationToken);
        await CatalogSeeder.SeedAsync(db, cancellationToken);
    }
}
