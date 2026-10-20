using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loja.Catalog.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool to create migrations. Generating a migration does not
/// connect to the database, so the connection string is a placeholder.
/// </summary>
internal sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql("Host=design-time-only", CatalogDatabase.ConfigureNpgsql)
            .Options;

        return new CatalogDbContext(options);
    }
}
