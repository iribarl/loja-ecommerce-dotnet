using Loja.Payments.Domain;

namespace Loja.Payments.Application;

public interface IPaymentRepository
{
    /// <summary>The most recent payment of an order, if any.</summary>
    Task<Payment?> GetLatestForOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Payment?> GetByExternalIdAsync(string gateway, string externalId, CancellationToken cancellationToken);

    /// <summary>Pending payments whose Pix expired before <paramref name="now"/>, oldest first.</summary>
    Task<IReadOnlyList<Payment>> ListExpiredPendingAsync(DateTimeOffset now, int max, CancellationToken cancellationToken);

    void Add(Payment payment);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<bool> WasWebhookProcessedAsync(string gateway, string eventId, CancellationToken cancellationToken);

    /// <summary>Records the webhook delivery; it is saved together with the payment changes.</summary>
    void RecordWebhook(string gateway, string eventId, DateTimeOffset now);

    /// <summary>
    /// Saves the payment changes and the webhook record in one transaction.
    /// False when the same delivery was recorded meanwhile by a concurrent request.
    /// </summary>
    Task<bool> SaveWebhookAsync(CancellationToken cancellationToken);
}
