using Loja.Payments.Application.ExpirePixCharges;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loja.Payments.Infrastructure;

/// <summary>
/// Runs <see cref="ExpirePixChargesHandler"/> every 30 seconds, in its own scope each time.
/// </summary>
internal sealed partial class PixExpirySweeper(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<PixExpirySweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<ExpirePixChargesHandler>().HandleAsync(stoppingToken);
                if (expired > 0)
                {
                    Expired(logger, expired);
                }

                await Task.Delay(Interval, clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // E.g. the provider or the database is briefly down: try again on the next round.
                SweepFailed(logger, ex);
                await Task.Delay(Interval, clock, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Expired {Count} unpaid Pix charges.")]
    private static partial void Expired(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Pix expiry sweep failed.")]
    private static partial void SweepFailed(ILogger logger, Exception exception);
}
