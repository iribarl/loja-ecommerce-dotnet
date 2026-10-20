using Microsoft.Extensions.Logging;

namespace Loja.Catalog.Infrastructure;

/// <summary>
/// Log messages of the Catalog module. The source generator writes the implementation at compile time,
/// so logging allocates nothing when the level is disabled.
/// </summary>
internal static partial class CatalogLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Catalog database not reachable (attempt {Attempt}/{MaxAttempts}); retrying in {Delay}.")]
    public static partial void DatabaseNotReachable(ILogger logger, Exception exception, int attempt, int maxAttempts, TimeSpan delay);
}
