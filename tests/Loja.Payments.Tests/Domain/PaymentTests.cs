using Loja.Payments.Domain;
using Loja.SharedKernel;

namespace Loja.Payments.Tests.Domain;

public class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Money Amount = Money.Of(342.60m);

    [Fact]
    public void Create_IsPending_AndAnnouncesTheCharge()
    {
        var payment = NewPayment();

        payment.Status.ShouldBe(PaymentStatus.Pending);
        var created = payment.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<PixChargeCreated>();
        created.ExpiresAt.ShouldBe(Now.AddMinutes(30));
    }

    [Fact]
    public void ConfirmPaid_WithTheRightAmount_MarksPaid()
    {
        var payment = NewPayment();
        payment.ClearDomainEvents();

        payment.ConfirmPaid(Amount, Now.AddMinutes(5)).IsSuccess.ShouldBeTrue();

        payment.Status.ShouldBe(PaymentStatus.Paid);
        payment.PaidAt.ShouldBe(Now.AddMinutes(5));
        payment.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<PaymentConfirmed>();
    }

    [Fact]
    public void ConfirmPaid_Twice_IsHarmless_AndAnnouncesOnlyOnce()
    {
        var payment = NewPayment();
        payment.ClearDomainEvents();

        payment.ConfirmPaid(Amount, Now);
        payment.ConfirmPaid(Amount, Now).IsSuccess.ShouldBeTrue();

        payment.DomainEvents.Count.ShouldBe(1);
    }

    [Fact]
    public void ConfirmPaid_WithAnotherAmount_IsRejected()
    {
        var payment = NewPayment();

        payment.ConfirmPaid(Money.Of(1m), Now).Error.ShouldBe(PaymentErrors.AmountMismatch);
        payment.Status.ShouldBe(PaymentStatus.Pending);
    }

    [Fact]
    public void Expire_AfterTheDeadline_CancelsThePayment()
    {
        var payment = NewPayment();
        payment.ClearDomainEvents();

        payment.Expire(Now.AddMinutes(31)).IsSuccess.ShouldBeTrue();

        payment.Status.ShouldBe(PaymentStatus.Expired);
        payment.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<PaymentExpired>();
    }

    [Fact]
    public void Expire_BeforeTheDeadline_IsRejected()
    {
        NewPayment().Expire(Now.AddMinutes(10)).Error.ShouldBe(PaymentErrors.NotExpiredYet);
    }

    [Fact]
    public void PaidPayment_DoesNotExpire()
    {
        var payment = NewPayment();
        payment.ConfirmPaid(Amount, Now);

        payment.Expire(Now.AddHours(1)).Error.ShouldBe(PaymentErrors.NotPending);
        payment.Status.ShouldBe(PaymentStatus.Paid);
    }

    [Fact]
    public void ExpiredPayment_CannotBeConfirmed()
    {
        var payment = NewPayment();
        payment.Expire(Now.AddHours(1));

        payment.ConfirmPaid(Amount, Now.AddHours(2)).Error.ShouldBe(PaymentErrors.PaidAfterExpiry);
    }

    private static Payment NewPayment() =>
        Payment.Create(Guid.NewGuid(), "fake", Amount,
            new PixCharge("ext-1", "00020126...", "data:image/svg+xml;base64,AAA", Now.AddMinutes(30)), Now);
}
