using Loja.Catalog.Infrastructure;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Catalog.Infrastructure.Persistence.Seed;
using Loja.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

    public PostgresFixture()
    {
        _web = new(() => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(web => web.UseSetting(
                $"ConnectionStrings:{CatalogModule.ConnectionStringName}",
                _container.GetConnectionString())));
    }

    /// <summary>The real app (Blazor pages + HTTP API), created on first use and shared by all tests.</summary>
    public WebApplicationFactory<Program> Web => _web.Value;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateCatalogDbContext();
        await db.Database.MigrateAsync();
        await CatalogSeeder.SeedAsync(db, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_web.IsValueCreated)
        {
            await _web.Value.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    internal CatalogDbContext CreateCatalogDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_container.GetConnectionString(), CatalogDatabase.ConfigureNpgsql)
            .Options);
}
