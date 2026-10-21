using Loja.Payments.Application;
using Loja.Payments.Domain;
using Microsoft.EntityFrameworkCore;

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
}
