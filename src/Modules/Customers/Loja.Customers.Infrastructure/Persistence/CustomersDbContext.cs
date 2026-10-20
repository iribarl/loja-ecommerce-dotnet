using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Loja.Customers.Infrastructure.Persistence;

/// <summary>
/// Identity's tables (users, logins, tokens...) in the module's own "customers" schema.
/// </summary>
public sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options)
    : IdentityDbContext<Customer, IdentityRole<Guid>, Guid>(options)
{
    public const string Schema = "customers";

    /// <summary>Npgsql options shared by the app, the design-time factory and the tests.</summary>
    public static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<Customer>(customer =>
        {
            customer.Property(c => c.FullName).HasMaxLength(150);
            customer.Ignore(c => c.FirstName);
        });
    }
}
