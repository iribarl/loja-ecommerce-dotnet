using Loja.Shared.Infrastructure.Persistence;
using Loja.Cart.Infrastructure.Persistence;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Catalog.Infrastructure.Persistence.Seed;
using Loja.Customers.Infrastructure.Persistence;
using Loja.IntegrationTests;
using Loja.Shipping.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

// One container for the whole test run: starting Postgres takes seconds, the tests only read.
[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Loja.IntegrationTests;

/// <summary>
/// Starts a real PostgreSQL in Docker, applies the migrations and loads the demo seed.
/// Also hosts the real web app in memory against that database.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as the Aspire AppHost, so tests run against the production engine version.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18.3").Build();

    // Lazy<T> is thread-safe: test classes run in parallel and must share a single app.
    private readonly Lazy<WebApplicationFactory<Program>> _web;
    private readonly Lazy<WebApplicationFactory<Program>> _shoppingWeb;

    public PostgresFixture()
    {
        _web = new(() => App(_container.GetConnectionString()));
        _shoppingWeb = new(() => App(DatabaseConnection("loja_e2e")));
    }

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>The real app (Blazor pages + HTTP API), created on first use and shared by all tests.</summary>
    public WebApplicationFactory<Program> Web => _web.Value;

    /// <summary>
    /// Another instance of the real app on its own database ("loja_e2e"), for end-to-end tests that
    /// buy things and change stock. On startup (Development) it migrates every module and seeds the catalog.
    /// </summary>
    public WebApplicationFactory<Program> ShoppingWeb => _shoppingWeb.Value;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateCatalogDbContext();
        await db.Database.MigrateAsync();
        await CatalogSeeder.SeedAsync(db, CancellationToken.None);

        await using var cartDb = CreateCartDbContext();
        await cartDb.Database.MigrateAsync();

        await using var customersDb = new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(_container.GetConnectionString(), CustomersDbContext.ConfigureNpgsql)
            .Options);
        await customersDb.Database.MigrateAsync();

        // A second database in the same container, without the demo seed, for tests that create and
        // change catalog data (stock): they must not disturb tests that count the seed's products.
        await using var scratch = CreateScratchCatalogDbContext();
        await scratch.Database.MigrateAsync(); // creates the database too
    }

    public async ValueTask DisposeAsync()
    {
        if (_web.IsValueCreated)
        {
            await _web.Value.DisposeAsync();
        }

        if (_shoppingWeb.IsValueCreated)
        {
            await _shoppingWeb.Value.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    internal CatalogDbContext CreateCatalogDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_container.GetConnectionString(), CatalogDatabase.ConfigureNpgsql)
            .Options);

    /// <summary>Catalog in the "scratch" database: empty catalog, free to change.</summary>
    internal CatalogDbContext CreateScratchCatalogDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(DatabaseConnection("loja_scratch"), CatalogDatabase.ConfigureNpgsql)
            .Options);

    private string DatabaseConnection(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;

    // The server is started here, inside the Lazy: WebApplicationFactory starts it on first use, and two
    // test classes doing that at the same time would start two hosts that migrate the database at once.
    /// <summary>
    /// A separate instance of the app on its own database in this container, optionally with Redis
    /// as the second-level cache (as in the AppHost). The caller disposes it.
    /// </summary>
    internal WebApplicationFactory<Program> CreateApp(string database, string? redisConnectionString = null) =>
        App(DatabaseConnection(database), redisConnectionString);

    private static WebApplicationFactory<Program> App(string connectionString, string? redisConnectionString = null)
    {
        var app = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(web => web
                .UseSetting($"ConnectionStrings:{LojaDatabase.ConnectionStringName}", connectionString)
                .UseSetting("ConnectionStrings:redis", redisConnectionString ?? string.Empty)
                // Uploaded images go to a temporary folder, not into the source tree.
                .UseSetting("Catalog:Images:Path", Path.Combine(Path.GetTempPath(), "loja-tests", Guid.NewGuid().ToString("N")))
                // External services are replaced: tests never depend on the internet.
                .ConfigureTestServices(services => services.AddSingleton<IAddressLookup, FakeAddressLookup>()));
        _ = app.Server;
        return app;
    }

    internal CartDbContext CreateCartDbContext() =>
        new(new DbContextOptionsBuilder<CartDbContext>()
            .UseNpgsql(_container.GetConnectionString(), CartDbContext.ConfigureNpgsql)
            .Options);
}
