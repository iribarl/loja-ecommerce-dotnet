using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loja.Cart.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool to create migrations; the connection string is a placeholder.
/// </summary>
internal sealed class CartDbContextFactory : IDesignTimeDbContextFactory<CartDbContext>
{
    public CartDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CartDbContext>()
            .UseNpgsql("Host=design-time-only", CartDbContext.ConfigureNpgsql)
            .Options);
}
