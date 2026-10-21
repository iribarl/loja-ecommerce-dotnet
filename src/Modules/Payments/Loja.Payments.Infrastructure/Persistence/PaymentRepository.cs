using Loja.Payments.Application;
using Loja.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Loja.Payments.Infrastructure.Persistence;

internal sealed class PaymentRepository(PaymentsDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetLatestForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.Payments.Where(p => p.OrderId == orderId).OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public Task<Payment?> GetByExternalIdAsync(string gateway, string externalId, CancellationToken cancellationToken) =>
        db.Payments.FirstOrDefaultAsync(p => p.Gateway == gateway && p.ExternalId == externalId, cancellationToken);

    public async Task<IReadOnlyList<Payment>> ListExpiredPendingAsync(DateTimeOffset now, int max, CancellationToken cancellationToken) =>
        await db.Payments
            .Where(p => p.Status == PaymentStatus.Pending && p.ExpiresAt <= now)
            .OrderBy(p => p.ExpiresAt)
            .Take(max)
            .ToListAsync(cancellationToken);

    public void Add(Payment payment) => db.Payments.Add(payment);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public Task<bool> WasWebhookProcessedAsync(string gateway, string eventId, CancellationToken cancellationToken) =>
        db.ProcessedWebhooks.AnyAsync(w => w.Gateway == gateway && w.EventId == eventId, cancellationToken);

    public void RecordWebhook(string gateway, string eventId, DateTimeOffset now) =>
        db.ProcessedWebhooks.Add(new ProcessedWebhook { Gateway = gateway, EventId = eventId, ReceivedAt = now });

    public async Task<bool> SaveWebhookAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request recorded the same delivery a moment earlier: nothing of ours was saved.
            return false;
        }
    }
}
