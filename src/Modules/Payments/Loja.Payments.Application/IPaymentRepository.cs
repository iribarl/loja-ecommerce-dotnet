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
}
