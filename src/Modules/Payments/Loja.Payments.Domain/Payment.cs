using Loja.SharedKernel;

namespace Loja.Payments.Domain;

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.CreateVersion7());
}

public enum PaymentStatus
{
    /// <summary>The Pix charge exists and nobody paid it yet.</summary>
    Pending,

    /// <summary>The gateway confirmed the payment.</summary>
    Paid,

    /// <summary>Not paid in time: the order is cancelled and the stock released.</summary>
    Expired,
}

/// <summary>What the gateway returns when a Pix charge is created.</summary>
/// <param name="ExternalId">The gateway's own id of the charge.</param>
/// <param name="CopyPaste">"Pix copia e cola": the EMV text the shopper pastes in their bank app.</param>
/// <param name="QrCodeImage">The QR code as a data URI (e.g. "data:image/png;base64,...").</param>
public sealed record PixCharge(string ExternalId, string CopyPaste, string QrCodeImage, DateTimeOffset ExpiresAt);

/// <summary>
/// A Pix payment of one order. Created when the charge is requested; becomes Paid only after the
/// gateway confirms it, or Expired when the time is up.
/// </summary>
public sealed class Payment : Entity<PaymentId>
{
    private Payment(PaymentId id, Guid orderId, string gateway, Money amount, PixCharge charge, DateTimeOffset now)
        : base(id)
    {
        OrderId = orderId;
        Gateway = gateway;
        Amount = amount;
        ExternalId = charge.ExternalId;
        PixCopyPaste = charge.CopyPaste;
        PixQrCodeImage = charge.QrCodeImage;
        ExpiresAt = charge.ExpiresAt;
        CreatedAt = now;
        Status = PaymentStatus.Pending;
    }

    // For EF Core materialization: it cannot pass Money (a complex type) to the constructor.
    private Payment()
        : base(default)
    {
        Gateway = null!;
        Amount = null!;
        ExternalId = null!;
        PixCopyPaste = null!;
        PixQrCodeImage = null!;
    }

    /// <summary>The Orders module's order; Payments knows it only as an id.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Which gateway handles it, e.g. "fake" or "mercadopago".</summary>
    public string Gateway { get; private set; }

    public string ExternalId { get; private set; }

    public Money Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string PixCopyPaste { get; private set; }

    public string PixQrCodeImage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public static Payment Create(Guid orderId, string gateway, Money amount, PixCharge charge, DateTimeOffset now)
    {
        var payment = new Payment(PaymentId.New(), orderId, gateway, amount, charge, now);
        payment.Raise(new PixChargeCreated(payment.Id, orderId, charge.ExpiresAt));
        return payment;
    }

    /// <summary>
    /// The gateway says the charge was paid. Confirming twice is harmless (webhooks repeat).
    /// The paid amount must be exactly the charged amount.
    /// </summary>
    public Result ConfirmPaid(Money paidAmount, DateTimeOffset now)
    {
        if (Status == PaymentStatus.Paid)
        {
            return Result.Success();
        }

        if (Status == PaymentStatus.Expired)
        {
            // The gateway should refuse late payments; if one arrives, a person refunds it.
            return PaymentErrors.PaidAfterExpiry;
        }

        if (paidAmount != Amount)
        {
            return PaymentErrors.AmountMismatch;
        }

        Status = PaymentStatus.Paid;
        PaidAt = now;
        Raise(new PaymentConfirmed(Id, OrderId, Amount));
        return Result.Success();
    }

    /// <summary>Not paid in time. Only pending payments expire.</summary>
    public Result Expire(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
        {
            return PaymentErrors.NotPending;
        }

        if (now < ExpiresAt)
        {
            return PaymentErrors.NotExpiredYet;
        }

        Status = PaymentStatus.Expired;
        Raise(new PaymentExpired(Id, OrderId));
        return Result.Success();
    }
}

public sealed record PixChargeCreated(PaymentId PaymentId, Guid OrderId, DateTimeOffset ExpiresAt) : DomainEvent;

public sealed record PaymentConfirmed(PaymentId PaymentId, Guid OrderId, Money Amount) : DomainEvent;

public sealed record PaymentExpired(PaymentId PaymentId, Guid OrderId) : DomainEvent;

public static class PaymentErrors
{
    public static readonly Error AmountMismatch =
        Error.Conflict("Payments.AmountMismatch", "O valor pago é diferente do valor do pedido.");

    public static readonly Error PaidAfterExpiry =
        Error.Conflict("Payments.PaidAfterExpiry", "O pagamento chegou depois do vencimento do Pix.");

    public static readonly Error NotPending =
        Error.Conflict("Payments.NotPending", "Este pagamento não está mais pendente.");

    public static readonly Error NotExpiredYet =
        Error.Validation("Payments.NotExpiredYet", "Este Pix ainda não venceu.");

    public static readonly Error NotFound =
        Error.NotFound("Payments.NotFound", "Pagamento não encontrado.");
}
