using Loja.Orders.Domain;
using Loja.SharedKernel;

namespace Loja.Orders.Tests.Domain;

public class OrderTests
{
    private static readonly DateTimeOffset Now = OrderBuilder.Now;

    [Fact]
    public void Place_StartsAsCreated_WithTotalsAndAnEvent()
    {
        var order = OrderBuilder.Placed(OrderBuilder.Line(49.90m, 2), OrderBuilder.Line(229.90m, 1));

        order.Status.ShouldBe(OrderStatus.Created);
        order.Subtotal.ShouldBe(Money.Of(329.70m));
        order.Total.ShouldBe(Money.Of(342.60m)); // + 12.90 shipping
        order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderPlaced>().OrderId.ShouldBe(order.Id);
        order.History.ShouldHaveSingleItem().Status.ShouldBe(OrderStatus.Created);
    }

    [Fact]
    public void Place_WithoutLines_Fails()
    {
        Place([]).Error.ShouldBe(OrderErrors.NoLines);
    }

    [Fact]
    public void Place_WithZeroQuantity_Fails()
    {
        Place([OrderBuilder.Line(quantity: 0)]).Error.ShouldBe(OrderErrors.InvalidQuantity);
    }

    [Fact]
    public void Place_WithMixedCurrencies_Fails()
    {
        var dollars = OrderBuilder.Line() with { UnitPrice = Money.Of(10m, "USD") };

        Place([OrderBuilder.Line(), dollars]).Error.ShouldBe(OrderErrors.MixedCurrencies);
    }

    [Fact]
    public void Cancel_KeepsTheReason_AndRaisesTheEvent()
    {
        var order = OrderBuilder.InStatus(OrderStatus.AwaitingPayment);

        order.Cancel("Comprei errado", Now).IsSuccess.ShouldBeTrue();

        order.CancellationReason.ShouldBe("Comprei errado");
        var cancelled = order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderCancelled>();
        cancelled.Expired.ShouldBeFalse();
    }

    [Fact]
    public void Expire_IsACancellationMarkedAsExpired()
    {
        var order = OrderBuilder.InStatus(OrderStatus.AwaitingPayment);

        order.Expire(Now).IsSuccess.ShouldBeTrue();

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<OrderCancelled>().Expired.ShouldBeTrue();
    }

    [Fact]
    public void LinesKeepThePriceOfTheMomentOfPurchase()
    {
        var line = OrderBuilder.Line(59.90m, 3);
        var order = OrderBuilder.Placed(line);

        order.Lines.ShouldHaveSingleItem().LineTotal.ShouldBe(Money.Of(179.70m));
    }

    [Fact]
    public void ShortCode_IsEightUppercaseCharacters()
    {
        var code = OrderId.New().ShortCode;

        code.Length.ShouldBe(8);
        code.ShouldBe(code.ToUpperInvariant());
    }

    [Theory]
    [InlineData("01310-100", "SP", true)]
    [InlineData("01310-100", "sp", true)]
    [InlineData("01310-100", "XX", false)]
    [InlineData("123", "SP", false)]
    public void ShippingAddress_ValidatesCepAndState(string cep, string state, bool valid)
    {
        var address = ShippingAddress.Create("Maria", cep, "Rua A", "10", null, "Centro", "São Paulo", state);

        address.IsSuccess.ShouldBe(valid);
        if (valid)
        {
            address.Value.State.ShouldBe("SP");
        }
    }

    [Fact]
    public void ShippingAddress_RequiresTheMainFields()
    {
        ShippingAddress.Create("Maria", "01310-100", " ", "10", null, "Centro", "São Paulo", "SP")
            .Error.ShouldBe(OrderErrors.AddressIncomplete);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ship_WithoutTrackingCode_FailsAndStaysInPicking(string trackingCode)
    {
        var order = OrderBuilder.InStatus(OrderStatus.Picking);

        order.Ship(trackingCode, Now).Error.ShouldBe(OrderErrors.TrackingCodeRequired);

        order.Status.ShouldBe(OrderStatus.Picking);
        order.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Ship_KeepsTheTrimmedTrackingCode()
    {
        var order = OrderBuilder.InStatus(OrderStatus.Picking);

        order.Ship("  BR123456789BR ", Now).IsSuccess.ShouldBeTrue();

        order.TrackingCode.ShouldBe("BR123456789BR");
    }

    [Fact]
    public void Cancel_WithoutReason_Fails()
    {
        var order = OrderBuilder.InStatus(OrderStatus.AwaitingPayment);

        order.Cancel(" ", Now).Error.ShouldBe(OrderErrors.CancellationReasonRequired);

        order.Status.ShouldBe(OrderStatus.AwaitingPayment);
    }

    private static Result<Order> Place(OrderLine[] lines) =>
        Order.Place(OrderId.New(), new CustomerSnapshot(Guid.NewGuid(), "Maria", "maria@exemplo.com"),
            lines, OrderBuilder.Address(), OrderBuilder.Shipping(), Now);
}
