using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Loja.Shared.Infrastructure.Persistence;

/// <summary>
/// The single PostgreSQL database of the monolith. Each module owns a schema inside it.
/// </summary>
public static partial class LojaDatabase
{
    /// <summary>Name of the connection string injected by the Aspire AppHost.</summary>
    public const string ConnectionStringName = "lojadb";

    /// <summary>
    /// Applies pending migrations, retrying transient errors. While the environment starts, Postgres
    /// (or even resolving "localhost") can fail for a few seconds; without retries an unhandled
    /// exception at startup stops the whole app. A non-transient error (e.g. a broken migration)
    /// fails at once.
    /// </summary>
    public static async Task MigrateWithRetryAsync(
        this DbContext db,
        ILogger logger,
        CancellationToken cancellationToken,
        int maxAttempts = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(2 * attempt);
                DatabaseNotReachable(logger, ex, db.GetType().Name, attempt, maxAttempts, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception ex) =>
        ex is NpgsqlException { IsTransient: true }
        || (ex.InnerException is not null && IsTransient(ex.InnerException));

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "{Context}: database not reachable (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}.")]
    private static partial void DatabaseNotReachable(
        ILogger logger, Exception exception, string context, int attempt, int maxAttempts, TimeSpan delay);
}
