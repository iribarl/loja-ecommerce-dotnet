using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loja.Catalog.Infrastructure.Contracts;

/// <summary>
/// Returns to stock the reservations nobody resolved in time, e.g. when the app stopped between
/// reserving the stock and saving the order. Without it those units would be lost forever.
/// </summary>
internal sealed partial class ExpiredReservationsSweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<ExpiredReservationsSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var reservations = scope.ServiceProvider.GetRequiredService<StockReservations>();
                var released = await reservations.ReleaseExpiredAsync(stoppingToken);
                if (released > 0)
                {
                    Released(logger, released);
                }

                await Task.Delay(Interval, clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                SweepFailed(logger, ex);
                await Task.Delay(Interval, clock, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Released {Count} expired stock reservations.")]
    private static partial void Released(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Expired reservations sweep failed.")]
    private static partial void SweepFailed(ILogger logger, Exception exception);
}
