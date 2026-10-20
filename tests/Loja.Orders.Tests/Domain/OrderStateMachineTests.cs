using Loja.Orders.Domain;
using Loja.SharedKernel;

namespace Loja.Orders.Tests.Domain;

/// <summary>
/// Every action from every status: only the transitions in the diagram succeed.
/// </summary>
public class OrderStateMachineTests
{
    private static readonly DateTimeOffset Now = OrderBuilder.Now;

    // Action name → (what it does, the statuses it may start from, where it leads).
    private static readonly Dictionary<string, (Func<Order, Result> Run, OrderStatus[] From, OrderStatus To)> Actions = new()
    {
        ["AwaitPayment"] = (o => o.AwaitPayment(Now), [OrderStatus.Created], OrderStatus.AwaitingPayment),
        ["MarkPaid"] = (o => o.MarkPaid(Now), [OrderStatus.AwaitingPayment], OrderStatus.Paid),
        ["StartPicking"] = (o => o.StartPicking(Now), [OrderStatus.Paid], OrderStatus.Picking),
        ["Ship"] = (o => o.Ship("BR123", Now), [OrderStatus.Picking], OrderStatus.Shipped),
        ["Deliver"] = (o => o.Deliver(Now), [OrderStatus.Shipped], OrderStatus.Delivered),
        ["Cancel"] = (o => o.Cancel("Desisti", Now), [OrderStatus.Created, OrderStatus.AwaitingPayment], OrderStatus.Cancelled),
        ["Expire"] = (o => o.Expire(Now), [OrderStatus.AwaitingPayment], OrderStatus.Cancelled),
        ["Refund"] = (o => o.Refund(Now), [OrderStatus.Paid], OrderStatus.Refunded),
    };

    public static TheoryData<string, OrderStatus> AllActionsFromAllStatuses()
    {
        var data = new TheoryData<string, OrderStatus>();
        foreach (var action in Actions.Keys)
        {
            foreach (var status in Enum.GetValues<OrderStatus>())
            {
                data.Add(action, status);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllActionsFromAllStatuses))]
    public void Transition_SucceedsOnlyFromAllowedStatuses(string action, OrderStatus from)
    {
        var order = OrderBuilder.InStatus(from);
        var historyBefore = order.History.Count;
        var (run, allowedFrom, to) = Actions[action];

        var result = run(order);

        if (allowedFrom.Contains(from))
        {
            result.IsSuccess.ShouldBeTrue();
            order.Status.ShouldBe(to);
            order.History[^1].Status.ShouldBe(to);
        }
        else
        {
            result.Error.Code.ShouldBe("Orders.InvalidTransition");
            order.Status.ShouldBe(from);
            order.History.Count.ShouldBe(historyBefore);
            order.DomainEvents.ShouldBeEmpty();
        }
    }

    [Fact]
    public void FullHappyPath_RecordsEveryStepInTheHistory()
    {
        var order = OrderBuilder.InStatus(OrderStatus.Delivered);

        order.History.Select(h => h.Status).ShouldBe(
        [
            OrderStatus.Created, OrderStatus.AwaitingPayment, OrderStatus.Paid,
            OrderStatus.Picking, OrderStatus.Shipped, OrderStatus.Delivered,
        ]);
        order.TrackingCode.ShouldBe("BR123456789");
    }

    [Theory]
    [InlineData(OrderStatus.Created, true)]
    [InlineData(OrderStatus.AwaitingPayment, true)]
    [InlineData(OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Shipped, false)]
    public void CustomerCanCancel_OnlyBeforePayment(OrderStatus status, bool expected)
    {
        OrderBuilder.InStatus(status).CanBeCancelledByCustomer.ShouldBe(expected);
    }
}
