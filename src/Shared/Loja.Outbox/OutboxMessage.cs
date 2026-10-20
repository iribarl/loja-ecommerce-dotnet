namespace Loja.Outbox;

/// <summary>
/// An integration event waiting to be delivered. Written in the same transaction as the change
/// that caused it, so a committed change always has its event and a rolled-back one never does.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>"Full.Type.Name, Assembly" of the integration event.</summary>
    public required string Type { get; init; }

    /// <summary>The event as JSON.</summary>
    public required string Content { get; init; }

    public DateTimeOffset OccurredOnUtc { get; init; }

    /// <summary>Null while pending.</summary>
    public DateTimeOffset? ProcessedOnUtc { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    /// <summary>
    /// After a failure, not before this time (exponential backoff): a consumer that failed because
    /// something was briefly down gets time to recover instead of burning its retries at once.
    /// </summary>
    public DateTimeOffset? NextAttemptOnUtc { get; set; }
}

/// <summary>
/// Records that a consumer already handled a message, so a redelivered message has no effect.
/// Lives in the consumer's module and is saved in the same transaction as the consumer's changes.
/// </summary>
public sealed class InboxMessage
{
    public Guid MessageId { get; init; }

    public required string Consumer { get; init; }

    public DateTimeOffset ProcessedOnUtc { get; init; }
}
