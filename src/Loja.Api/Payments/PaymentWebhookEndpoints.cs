using Loja.Payments.Application;
using Loja.Payments.Application.HandleWebhook;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Loja.Api.Payments;

public static class PaymentWebhookEndpoints
{
    /// <summary>Payment provider notifications are tiny; anything bigger is not a real webhook.</summary>
    private const int MaxBodyBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapPaymentWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        // One route per provider (/api/webhooks/mercadopago, /api/webhooks/fake): each validates its own signature.
        app.MapPost("/webhooks/{gateway}", ReceiveAsync)
            .WithTags("Payments")
            .WithName("PaymentWebhook")
            .WithSummary("Notification from a payment provider (signed).")
            .AllowAnonymous()
            .DisableAntiforgery(); // a server-to-server call: authenticity comes from the signature

        return app;
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult, NotFound, StatusCodeHttpResult>> ReceiveAsync(
        string gateway,
        HttpRequest request,
        HandlePaymentWebhookHandler handler,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaxBodyBytes)
        {
            return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var webhook = new WebhookRequest(
            request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value.ToString()),
            request.Query.ToDictionary(q => q.Key, q => q.Value.ToString()),
            body);

        // Errors while asking the provider bubble up as 500: the provider retries the notification later.
        return await handler.HandleAsync(gateway, webhook, cancellationToken) switch
        {
            WebhookOutcome.UnknownGateway => TypedResults.NotFound(),
            WebhookOutcome.Rejected => TypedResults.Unauthorized(),
            _ => TypedResults.Ok(), // accepted or duplicate: the provider must stop retrying
        };
    }
}
