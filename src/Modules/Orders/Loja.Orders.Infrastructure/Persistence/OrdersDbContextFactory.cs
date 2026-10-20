using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loja.Orders.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool to create migrations; the connection string is a placeholder.
/// </summary>
internal sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql("Host=design-time-only", OrdersDbContext.ConfigureNpgsql)
            .Options);
}
