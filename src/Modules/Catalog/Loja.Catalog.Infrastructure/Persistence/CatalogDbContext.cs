using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Loja.Catalog.Infrastructure.Persistence;

/// <summary>
/// The Catalog module's own database context. Its tables live in the "catalog" schema;
/// other modules never read them directly.
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
