using Loja.Outbox;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Loja.IntegrationTests.Outbox;

public sealed class OutboxTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaCreated;

    private ServiceProvider _services = default!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _services = new ServiceCollection()
            .AddDbContext<TestOutboxDbContext>(options => options
                .UseNpgsql(postgres.ConnectionString)
                .AddInterceptors(new OutboxInterceptor(new ParcelMapper())))
            .AddScoped<IIntegrationEventConsumer<ParcelShippedIntegrationEvent>, RecordingConsumer>()
            .BuildServiceProvider();

        await SchemaLock.WaitAsync(Ct);
        try
        {
            if (!_schemaCreated)
            {
                await using var scope = _services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TestOutboxDbContext>();
                // The database already has other schemas, so EnsureCreated would do nothing:
                // create just this model's tables.
                await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(Ct);
                _schemaCreated = true;
            }
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task Saving_WritesTheEventToTheOutbox_InTheSameTransaction()
    {
        var parcel = await ShipNewParcel();

        var message = await Message(parcel);
        message.Type.ShouldStartWith(typeof(ParcelShippedIntegrationEvent).FullName!);
        message.ProcessedOnUtc.ShouldBeNull();
    }

    [Fact]
    public async Task FailedTransaction_LeavesNoEventBehind()
    {
        var parcel = await ShipNewParcel();

        // Same id again: the insert fails, so neither the entity nor its event may be saved.
        await using (var db = NewContext())
        {
            var duplicate = new Parcel(parcel);
            duplicate.Ship();
            db.Parcels.Add(duplicate);
            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        }

        (await MessagesFor(parcel)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Processor_DeliversAndMarksAsProcessed()
    {
        var parcel = await ShipNewParcel();

        await ProcessAll(NewProcessor());

        RecordingConsumer.Received.ShouldContain(parcel);
        (await Message(parcel)).ProcessedOnUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task FailingConsumer_LeavesTheMessagePending_ThenItIsRetried()
    {
        var parcel = Guid.NewGuid();
        RecordingConsumer.FailFor[parcel] = true;
        await ShipNewParcel(parcel);

        var clock = new ShiftableClock();
        await ProcessAll(NewProcessor(clock));

        // One attempt only: the backoff keeps it from being retried right away in the same run.
        var failed = await Message(parcel);
        failed.ProcessedOnUtc.ShouldBeNull();
        failed.Attempts.ShouldBe(1);
        failed.LastError!.ShouldContain("Simulated consumer failure.");

        RecordingConsumer.FailFor.TryRemove(parcel, out _);
        await ProcessAll(NewProcessor(clock));
        (await Message(parcel)).ProcessedOnUtc.ShouldBeNull(); // still waiting for its backoff

        clock.Advance(OutboxProcessor<TestOutboxDbContext>.Backoff(1));
        await ProcessAll(NewProcessor(clock));

        (await Message(parcel)).ProcessedOnUtc.ShouldNotBeNull();
        RecordingConsumer.Received.Count(id => id == parcel).ShouldBe(1);
    }

    [Fact]
    public async Task TwoProcessorsAtOnce_NeverDeliverTheSameMessageTwice()
    {
        var parcels = new List<Guid>();
        for (var i = 0; i < 30; i++)
        {
            parcels.Add(await ShipNewParcel());
        }

        // Like two instances of the app polling the same outbox at the same time.
        var first = NewProcessor();
        var second = NewProcessor();
        await Task.WhenAll(ProcessAll(first), ProcessAll(second));

        foreach (var parcel in parcels)
        {
            RecordingConsumer.Received.Count(id => id == parcel).ShouldBe(1);
        }
    }

    [Fact]
    public async Task Inbox_RemembersProcessedMessages()
    {
        var messageId = Guid.NewGuid();
        await using (var db = NewContext())
        {
            (await db.HasProcessedAsync(messageId, "test-consumer", Ct)).ShouldBeFalse();
            db.MarkProcessed(messageId, "test-consumer", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        await using var check = NewContext();
        (await check.HasProcessedAsync(messageId, "test-consumer", Ct)).ShouldBeTrue();
        (await check.HasProcessedAsync(messageId, "another-consumer", Ct)).ShouldBeFalse();
    }

    private async Task<Guid> ShipNewParcel(Guid? id = null)
    {
        var parcel = new Parcel(id ?? Guid.NewGuid());
        parcel.Ship();
        await using var db = NewContext();
        db.Parcels.Add(parcel);
        await db.SaveChangesAsync(Ct);
        parcel.DomainEvents.ShouldBeEmpty(); // the interceptor moved them to the outbox
        return parcel.Id;
    }

    private async Task<OutboxMessage> Message(Guid parcel) => (await MessagesFor(parcel)).Single();

    // Filtered in memory: LIKE does not apply to a jsonb column, and the test table is small.
    private async Task<List<OutboxMessage>> MessagesFor(Guid parcel)
    {
        await using var db = NewContext();
        var all = await db.Set<OutboxMessage>().AsNoTracking().ToListAsync(Ct);
        return all.Where(m => m.Content.Contains(parcel.ToString(), StringComparison.Ordinal)).ToList();
    }

    private TestOutboxDbContext NewContext() =>
        _services.CreateScope().ServiceProvider.GetRequiredService<TestOutboxDbContext>();

    private OutboxProcessor<TestOutboxDbContext> NewProcessor(TimeProvider? clock = null) =>
        new(_services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OutboxOptions { BatchSize = 5 }),
            clock ?? TimeProvider.System,
            NullLogger<OutboxProcessor<TestOutboxDbContext>>.Instance);

    /// <summary>Real time plus an offset the test can move forward.</summary>
    private sealed class ShiftableClock : TimeProvider
    {
        private TimeSpan _offset;

        public void Advance(TimeSpan by) => _offset += by + TimeSpan.FromMilliseconds(100);

        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + _offset;
    }

    private static async Task ProcessAll(OutboxProcessor<TestOutboxDbContext> processor)
    {
        while (await processor.ProcessBatchAsync(Ct) > 0)
        {
        }
    }
}
