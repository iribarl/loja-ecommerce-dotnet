using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Loja.Catalog.Infrastructure.Persistence;

internal static class CatalogDatabase
{
    /// <summary>Npgsql options shared by the app, the design-time factory and the tests.</summary>
    public static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        // Each module keeps its own migration history inside its own schema.
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, CatalogDbContext.Schema);
}
