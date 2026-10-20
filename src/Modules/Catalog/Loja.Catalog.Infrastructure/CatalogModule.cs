using Loja.Catalog.Application.Categories;
using Loja.Catalog.Application.Categories.ListCategories;
using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.GetFilterOptions;
using Loja.Catalog.Application.Products.GetProductBySlug;
using Loja.Catalog.Application.Products.SearchProducts;
using Loja.Catalog.Infrastructure.Caching;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Catalog.Infrastructure.Persistence.Seed;
using Loja.Catalog.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Loja.Catalog.Infrastructure;

/// <summary>
/// Entry point of the Catalog module for the host: one call registers everything the module needs.
/// </summary>
public static class CatalogModule
{
    /// <summary>Name of the connection string injected by the Aspire AppHost.</summary>
    public const string ConnectionStringName = "lojadb";

    public static IHostApplicationBuilder AddCatalogModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<CatalogDbContext>(options => options
            .UseNpgsql(builder.Configuration.GetConnectionString(ConnectionStringName), CatalogDatabase.ConfigureNpgsql));

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
        const int maxAttempts = 5;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(CatalogModule));

        // While the environment starts, Postgres (or even resolving "localhost") can fail for a few
        // seconds. Without retries an unhandled exception here stops the whole app.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                break;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(2 * attempt);
                CatalogLog.DatabaseNotReachable(logger, ex, attempt, maxAttempts, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        await CatalogSeeder.SeedAsync(db, cancellationToken);
    }

    // Network and connection errors are transient; a broken migration is not and must fail fast.
    private static bool IsTransient(Exception ex) =>
        ex is NpgsqlException { IsTransient: true }
        || (ex.InnerException is not null && IsTransient(ex.InnerException));
}
