using Loja.Orders.Contracts;
using Loja.Payments.Application;
using Loja.Payments.Application.CreatePixCharge;
using Loja.Payments.Domain;
using Loja.SharedKernel;

namespace Loja.Payments.Tests.Application;

public class CreatePixChargeHandlerTests
{
    private readonly FakeOrders _orders = new();
    private readonly InMemoryPayments _payments = new();
    private readonly CountingGateway _gateway = new();
    private readonly Guid _customer = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CreatePixChargeHandler Handler => new(_orders, _payments, new SingleGateway(_gateway), TimeProvider.System);

    [Fact]
    public async Task CreatesTheChargeForTheOrderTotal()
    {
        var order = _orders.Add(_customer, total: 342.60m);

        var result = await Handler.HandleAsync(_customer, order, Ct);

        result.Value.Amount.ShouldBe(342.60m);
        result.Value.Status.ShouldBe("Pending");
        result.Value.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(29));
        _gateway.Created.ShouldBe(1);
    }

    [Fact]
    public async Task OpeningThePageAgain_ReturnsTheSameValidCharge()
    {
        var order = _orders.Add(_customer, total: 100m);

        var first = await Handler.HandleAsync(_customer, order, Ct);
        var second = await Handler.HandleAsync(_customer, order, Ct);

        second.Value.PaymentId.ShouldBe(first.Value.PaymentId);
        _gateway.Created.ShouldBe(1);
    }

    [Fact]
    public async Task AnotherCustomersOrder_IsNotFound()
    {
        var order = _orders.Add(Guid.NewGuid(), total: 100m);

        (await Handler.HandleAsync(_customer, order, Ct)).Error.ShouldBe(PaymentErrors.NotFound);
        _gateway.Created.ShouldBe(0);
    }

    [Fact]
    public async Task OrderNoLongerWaitingForPayment_IsRefused()
    {
        var order = _orders.Add(_customer, total: 100m, canBePaid: false);

        (await Handler.HandleAsync(_customer, order, Ct)).Error.ShouldBe(CreatePixChargeHandler.OrderCannotBePaid);
    }

    [Fact]
    public async Task GatewayDown_ReturnsAnErrorTheShopperCanRetry()
    {
        var order = _orders.Add(_customer, total: 100m);
        _gateway.Fail = true;

        (await Handler.HandleAsync(_customer, order, Ct)).Error.ShouldBe(CreatePixChargeHandler.GatewayUnavailable);
        _payments.Saved.ShouldBeEmpty();
    }

    private sealed class FakeOrders : IOrdersApi
    {
        private readonly Dictionary<Guid, OrderForPayment> _orders = [];

        public Guid Add(Guid customer, decimal total, bool canBePaid = true)
        {
            var id = Guid.NewGuid();
            _orders[id] = new OrderForPayment(id, customer, "cliente@exemplo.com", "ABCD1234", total, "BRL", canBePaid);
            return id;
        }

        public Task<OrderForPayment?> GetForPaymentAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult(_orders.GetValueOrDefault(orderId));
    }

    private sealed class InMemoryPayments : IPaymentRepository
    {
        public List<Payment> Saved { get; } = [];

        private readonly List<Payment> _pending = [];

        public Task<Payment?> GetLatestForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Where(p => p.OrderId == orderId).OrderByDescending(p => p.CreatedAt).FirstOrDefault());

        public Task<Payment?> GetByExternalIdAsync(string gateway, string externalId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.Find(p => p.Gateway == gateway && p.ExternalId == externalId));

        public Task<IReadOnlyList<Payment>> ListExpiredPendingAsync(DateTimeOffset now, int max, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Payment>>(Saved.Where(p => p.Status == PaymentStatus.Pending && p.ExpiresAt <= now).ToList());

        public void Add(Payment payment) => _pending.Add(payment);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved.AddRange(_pending);
            _pending.Clear();
            return Task.CompletedTask;
        }

        public Task<bool> WasWebhookProcessedAsync(string gateway, string eventId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void RecordWebhook(string gateway, string eventId, DateTimeOffset now)
        {
        }

        public Task<bool> SaveWebhookAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class CountingGateway : IPaymentGateway
    {
        public int Created { get; private set; }

        public bool Fail { get; set; }

        public string Name => "test";

        public Task<PixCharge> CreatePixChargeAsync(PixChargeRequest request, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new HttpRequestException("Gateway is down.");
            }

            Created++;
            return Task.FromResult(new PixCharge($"ext-{Created}", "000201...", "data:image/svg+xml;base64,AA", request.ExpiresAt));
        }

        public Task<GatewayPaymentStatus> GetPaymentAsync(string externalId, CancellationToken cancellationToken) =>
            Task.FromResult(new GatewayPaymentStatus(externalId, GatewayPaymentState.Pending, null));

        public WebhookNotification? ReadWebhook(WebhookRequest request) => null;
    }

    private sealed class SingleGateway(IPaymentGateway gateway) : IPaymentGatewayResolver
    {
        public IPaymentGateway Default => gateway;

        public IPaymentGateway? Find(string name) => name == gateway.Name ? gateway : null;
    }
}
