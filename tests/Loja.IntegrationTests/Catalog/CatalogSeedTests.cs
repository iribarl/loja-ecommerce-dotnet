using Loja.Catalog.Infrastructure.Persistence.Seed;
using Loja.Catalog.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Loja.IntegrationTests.Catalog;

public sealed class CatalogSeedTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seed_RunTwice_DoesNotDuplicateData()
    {
        await using var db = postgres.CreateCatalogDbContext();
        var before = await db.Products.CountAsync(Ct);

        await CatalogSeeder.SeedAsync(db, Ct);

        (await db.Products.CountAsync(Ct)).ShouldBe(before);
        before.ShouldBe(CatalogSeedData.Products.Length);
    }

    [Fact]
    public async Task Categories_AreListedByName()
    {
        await using var db = postgres.CreateCatalogDbContext();

        var categories = await new CategoryQueries(db).ListAsync(Ct);

        categories.Select(c => c.Slug).ShouldBe(
            ["acessorios", "calcas-e-bermudas", "camisetas", "casacos-e-moletons", "vestidos-e-saias"]);
    }
}
