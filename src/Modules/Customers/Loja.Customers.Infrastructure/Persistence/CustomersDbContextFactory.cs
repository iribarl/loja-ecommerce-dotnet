using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Loja.Customers.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool to create migrations; the connection string is a placeholder.
/// </summary>
internal sealed class CustomersDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql("Host=design-time-only", CustomersDbContext.ConfigureNpgsql)
            .Options);
}
