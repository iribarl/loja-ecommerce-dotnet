using System.Reflection;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Loja.Outbox;

public sealed class OutboxOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; set; } = 20;

    /// <summary>After this many failed deliveries a message is left aside (and logged) for a person to look at.</summary>
    public int MaxAttempts { get; set; } = 10;
}

/// <summary>
/// Delivers the pending messages of one module's outbox to the consumers registered in the app.
/// </summary>
/// <remarks>
/// Rows are locked with FOR UPDATE SKIP LOCKED: if the app runs on several instances, each one
/// takes different messages instead of delivering the same message twice.
/// Each consumer runs in its own DI scope (its own DbContext), so one consumer's failure does not
/// leave another's changes half-saved.
/// </remarks>
public sealed partial class OutboxProcessor<TContext>(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    TimeProvider clock,
    ILogger<OutboxProcessor<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private static readonly MethodInfo ConsumeMethod =
        typeof(OutboxProcessor<TContext>).GetMethod(nameof(ConsumeAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly OutboxOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Keep going while there is work; wait only when the outbox is empty.
                if (await ProcessBatchAsync(stoppingToken) == 0)
                {
                    await Task.Delay(_options.PollingInterval, clock, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // E.g. the database is briefly unreachable: log and try again later, never stop the loop.
                BatchFailed(logger, ex, typeof(TContext).Name);
                await Task.Delay(_options.PollingInterval, clock, stoppingToken);
            }
        }
    }

    /// <returns>How many messages were taken (delivered or failed).</returns>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var table = OutboxTable(db);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();

        // The table name comes from the EF model, never from user input; the values are parameters.
#pragma warning disable EF1002 // Interpolated table name is safe here: it comes from the model.
        var messages = await db.Set<OutboxMessage>()
            .FromSqlRaw(
                $$"""
                SELECT * FROM {{table}}
                WHERE "ProcessedOnUtc" IS NULL
                  AND "Attempts" < {0}
                  AND ("NextAttemptOnUtc" IS NULL OR "NextAttemptOnUtc" <= {1})
                ORDER BY "OccurredOnUtc"
                LIMIT {2}
                FOR UPDATE SKIP LOCKED
                """,
                _options.MaxAttempts,
                now,
                _options.BatchSize)
            .ToListAsync(cancellationToken);
#pragma warning restore EF1002

        foreach (var message in messages)
        {
            try
            {
                await DeliverAsync(message, cancellationToken);
                message.ProcessedOnUtc = clock.GetUtcNow();
                message.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.NextAttemptOnUtc = now + Backoff(message.Attempts);
                message.LastError = ex.ToString()[..Math.Min(ex.ToString().Length, 4000)];
                DeliveryFailed(logger, ex, message.Id, message.Type, message.Attempts, _options.MaxAttempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages.Count;
    }

    /// <summary>2 s, 4 s, 8 s... capped at 5 minutes.</summary>
    internal static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempts), TimeSpan.FromMinutes(5).TotalSeconds));

    private async Task DeliverAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var integrationEvent = IntegrationEventSerializer.Deserialize(message.Type, message.Content);
        var consume = ConsumeMethod.MakeGenericMethod(integrationEvent.GetType());

        await (Task)consume.Invoke(null, [scopeFactory, integrationEvent, cancellationToken])!;
    }

    // Generic so consumers are resolved by their exact event type.
    private static async Task ConsumeAsync<TEvent>(
        IServiceScopeFactory scopeFactory,
        TEvent integrationEvent,
        CancellationToken cancellationToken)
        where TEvent : IntegrationEvent
    {
        using var lookup = scopeFactory.CreateScope();
        var consumerCount = lookup.ServiceProvider.GetServices<IIntegrationEventConsumer<TEvent>>().Count();

        for (var index = 0; index < consumerCount; index++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var consumer = scope.ServiceProvider.GetServices<IIntegrationEventConsumer<TEvent>>().ElementAt(index);
            await consumer.ConsumeAsync(integrationEvent, cancellationToken);
        }
    }

    private static string OutboxTable(DbContext db)
    {
        var entity = db.Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException($"{db.GetType().Name} has no outbox: call AddOutboxAndInbox().");
        return $"\"{entity.GetSchema()}\".\"{entity.GetTableName()}\"";
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Context}: outbox batch failed.")]
    private static partial void BatchFailed(ILogger logger, Exception exception, string context);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} ({Type}) failed (attempt {Attempts}/{MaxAttempts}).")]
    private static partial void DeliveryFailed(
        ILogger logger, Exception exception, Guid messageId, string type, int attempts, int maxAttempts);
}

public static class OutboxServiceCollectionExtensions
{
    /// <summary>Runs the outbox processor of <typeparamref name="TContext"/> in the background.</summary>
    public static IServiceCollection AddOutboxProcessor<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddOptions<OutboxOptions>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<OutboxProcessor<TContext>>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor<TContext>>());
        return services;
    }
}
