using Loja.Catalog.Application.Categories;
using Loja.Catalog.Application.Categories.ListCategories;
using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.GetFilterOptions;
using Loja.Catalog.Application.Products.GetProductBySlug;
using Loja.Catalog.Application.Products.SearchProducts;
using Loja.Catalog.Contracts;
using Loja.Catalog.Infrastructure.Caching;
using Loja.Catalog.Infrastructure.Contracts;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Catalog.Infrastructure.Persistence.Seed;
using Loja.Catalog.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Loja.Shared.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Loja.Catalog.Infrastructure;

/// <summary>
/// Entry point of the Catalog module for the host: one call registers everything the module needs.
/// </summary>
public static class CatalogModule
{
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

        // Public contract for other modules.
        builder.Services.AddScoped<ICatalogApi, CatalogApi>();

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
