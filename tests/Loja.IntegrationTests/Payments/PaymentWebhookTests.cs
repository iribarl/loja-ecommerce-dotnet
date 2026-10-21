using System.Net;
using System.Text;
using Loja.Catalog.Domain.Stock;
using Loja.Catalog.Infrastructure.Persistence;
using Loja.Orders.Domain;
using Loja.Outbox;
using Loja.Payments.Application;
using Loja.Payments.Application.CreatePixCharge;
using Loja.Payments.Domain;
using Loja.Payments.Infrastructure.Gateways;
using Loja.Payments.Infrastructure.Persistence;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.IntegrationTests.Payments;

/// <summary>
/// The webhook as the provider calls it (HTTP, signed), on the real app.
/// </summary>
public sealed class PaymentWebhookTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServiceProvider Services => postgres.ShoppingWeb.Services;

    private FakePixGateway Gateway => Services.GetRequiredService<FakePixGateway>();

    [Fact]
    public async Task PaidWebhook_PaysTheOrder_AndConfirmsTheStock()
    {
        var (order, externalId) = await OrderWithPix();
        Gateway.MarkAsPaid(externalId);

        (await Send(Gateway.BuildWebhook(externalId))).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await StatusOfPayment(externalId)).ShouldBe(PaymentStatus.Paid);
        // PaymentConfirmed → Orders → OrderPaid → Catalog, all through the outbox.
        await ShoppingSteps.Eventually(async () => await ShoppingSteps.StatusOf(Services, order) == OrderStatus.Paid);
        await ShoppingSteps.Eventually(async () => (await Reservations(order)).All(s => s == ReservationStatus.Confirmed));
    }

    [Fact]
    public async Task SameWebhookManyTimesAtOnce_ConfirmsOnlyOnce()
    {
        var (_, externalId) = await OrderWithPix();
        Gateway.MarkAsPaid(externalId);
        var webhook = Gateway.BuildWebhook(externalId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Send(webhook)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        (await ConfirmationsInOutbox(externalId)).ShouldBe(1);
    }

    [Fact]
    public async Task ForgedSignature_IsRejected()
    {
        var (_, externalId) = await OrderWithPix();
        Gateway.MarkAsPaid(externalId);
        var webhook = Gateway.BuildWebhook(externalId);
        var forged = webhook with
        {
            Headers = new Dictionary<string, string>(webhook.Headers)
            {
                ["x-signature"] = WebhookSignature.Sign("not-the-secret", externalId, webhook.Headers["x-request-id"], DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            },
        };

        (await Send(forged)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await StatusOfPayment(externalId)).ShouldBe(PaymentStatus.Pending);
    }

    [Fact]
    public async Task WebhookSayingPaid_WhenTheProviderSaysNot_ChangesNothing()
    {
        // Authentic notification, but nobody paid: the provider's real state wins.
        var (order, externalId) = await OrderWithPix();

        (await Send(Gateway.BuildWebhook(externalId))).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await StatusOfPayment(externalId)).ShouldBe(PaymentStatus.Pending);
        await ShoppingSteps.Eventually(async () => await ShoppingSteps.StatusOf(Services, order) == OrderStatus.AwaitingPayment);
    }

    [Fact]
    public async Task PaymentOfAnotherAmount_IsNotConfirmed()
    {
        var (_, externalId) = await OrderWithPix();
        Gateway.MarkAsPaid(externalId, Money.Of(1m));

        (await Send(Gateway.BuildWebhook(externalId))).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await StatusOfPayment(externalId)).ShouldBe(PaymentStatus.Pending);
    }

    [Fact]
    public async Task UnknownGateway_IsNotFound()
    {
        var response = await postgres.ShoppingWeb.CreateClient()
            .PostAsync("/api/webhooks/banco-imaginario", new StringContent("{}", Encoding.UTF8, "application/json"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<(Guid Order, string ExternalId)> OrderWithPix()
    {
        var (customer, order) = await ShoppingSteps.PlacedOrder(Services);

        await using var scope = Services.CreateAsyncScope();
        var pix = await scope.ServiceProvider.GetRequiredService<CreatePixChargeHandler>().HandleAsync(customer, order, Ct);
        var paymentId = new PaymentId(pix.Value.PaymentId);
        var externalId = await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.Where(p => p.Id == paymentId).Select(p => p.ExternalId).SingleAsync(Ct);

        return (order, externalId);
    }

    // Sends the notification over HTTP exactly as the provider would.
    private async Task<HttpResponseMessage> Send(WebhookRequest webhook)
    {
        var query = string.Join("&", webhook.Query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}"));
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/{FakePixGateway.GatewayName}?{query}")
        {
            Content = new StringContent(webhook.Body, Encoding.UTF8, "application/json"),
        };
        foreach (var (name, value) in webhook.Headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return await postgres.ShoppingWeb.CreateClient().SendAsync(request, Ct);
    }

    private async Task<PaymentStatus> StatusOfPayment(string externalId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .Payments.Where(p => p.ExternalId == externalId).Select(p => p.Status).SingleAsync(Ct);
    }

    private async Task<int> ConfirmationsInOutbox(string externalId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var payment = await db.Payments.SingleAsync(p => p.ExternalId == externalId, Ct);
        var messages = await db.Set<OutboxMessage>().AsNoTracking().ToListAsync(Ct);

        return messages.Count(m => m.Type.Contains("PaymentConfirmed", StringComparison.Ordinal)
            && m.Content.Contains(payment.Id.Value.ToString(), StringComparison.Ordinal));
    }

    private async Task<List<ReservationStatus>> Reservations(Guid order)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .StockReservations.Where(r => r.OrderId == order).Select(r => r.Status).ToListAsync(Ct);
    }
}
