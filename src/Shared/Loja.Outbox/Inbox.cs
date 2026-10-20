using Microsoft.EntityFrameworkCore;

namespace Loja.Outbox;

/// <summary>
/// Idempotent consumers: the outbox delivers at least once, so a consumer may see a message twice
/// (e.g. another consumer of the same message failed and the message was retried).
/// </summary>
/// <example>
/// <code>
/// if (await db.HasProcessedAsync(message.Id, Name, ct)) return;
/// ... change the module's data ...
/// db.MarkProcessed(message.Id, Name, now);
/// await db.SaveChangesAsync(ct);   // the change and the inbox row commit together
/// </code>
/// </example>
public static class Inbox
{
    public static Task<bool> HasProcessedAsync(
        this DbContext db,
        Guid messageId,
        string consumer,
        CancellationToken cancellationToken) =>
        db.Set<InboxMessage>().AnyAsync(m => m.MessageId == messageId && m.Consumer == consumer, cancellationToken);

    public static void MarkProcessed(this DbContext db, Guid messageId, string consumer, DateTimeOffset now) =>
        db.Set<InboxMessage>().Add(new InboxMessage { MessageId = messageId, Consumer = consumer, ProcessedOnUtc = now });
}
