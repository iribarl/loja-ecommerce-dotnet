using Loja.Catalog.Infrastructure.Persistence;
using Loja.Catalog.Infrastructure.Persistence.Seed;
using Loja.IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

// One container for the whole test run: starting Postgres takes seconds, the tests only read.
[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace Loja.IntegrationTests;

/// <summary>
/// Starts a real PostgreSQL in Docker, applies the migrations and loads the demo seed.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as the Aspire AppHost, so tests run against the production engine version.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18.3").Build();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateCatalogDbContext();
        await db.Database.MigrateAsync();
        await CatalogSeeder.SeedAsync(db, CancellationToken.None);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    internal CatalogDbContext CreateCatalogDbContext() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(_container.GetConnectionString(), CatalogDatabase.ConfigureNpgsql)
            .Options);
}
