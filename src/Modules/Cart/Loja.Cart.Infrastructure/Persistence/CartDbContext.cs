using Loja.Cart.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Loja.Cart.Infrastructure.Persistence;

/// <summary>
/// The Cart module's own database context, in the "cart" schema.
/// </summary>
public sealed class CartDbContext(DbContextOptions<CartDbContext> options) : DbContext(options)
{
    public const string Schema = "cart";

    public DbSet<ShoppingCart> Carts => Set<ShoppingCart>();

    /// <summary>Npgsql options shared by the app, the design-time factory and the tests.</summary>
    public static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartDbContext).Assembly);
    }
}
