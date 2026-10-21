using Loja.Orders.Application.GetOrders;
using Loja.Payments.Infrastructure.Gateways;
using Loja.Web.Account;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Loja.Web.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app, IWebHostEnvironment environment)
    {
        // Polled by js/order.js on the order page to notice the payment without reloading by hand.
        app.MapGet("/pedidos/{orderId:guid}/status", async Task<Results<Ok<OrderStatusResponse>, NotFound>> (
                Guid orderId,
                HttpContext context,
                GetMyOrderHandler orders,
                CancellationToken cancellationToken) =>
            {
                var order = await orders.HandleAsync(CurrentCustomer.IdOf(context)!.Value, orderId, cancellationToken);
                return order.IsSuccess ? TypedResults.Ok(new OrderStatusResponse(order.Value.Status)) : TypedResults.NotFound();
            })
            .RequireAuthorization()
            .ExcludeFromDescription();

        if (!environment.IsProduction())
        {
            // Development only: "pay" a fake Pix, as the shopper would in their bank app.
            app.MapPost("/dev/pix/{paymentId:guid}/pagar", async Task<Results<RedirectHttpResult, BadRequest, NotFound>> (
                    Guid paymentId,
                    HttpContext context,
                    IAntiforgery antiforgery,
                    IPixPaymentSimulator simulator,
                    CancellationToken cancellationToken) =>
                {
                    if (!await antiforgery.IsRequestValidAsync(context))
                    {
                        return TypedResults.BadRequest();
                    }

                    if (!await simulator.SimulatePaymentAsync(paymentId, cancellationToken))
                    {
                        return TypedResults.NotFound();
                    }

                    // Back to the page the button was on (local paths only).
                    var returnUrl = context.Request.Form["returnUrl"].ToString();
                    return TypedResults.LocalRedirect(SafeRedirect.LocalOrHome(returnUrl));
                })
                .RequireAuthorization()
                .DisableAntiforgery() // validated above
                .ExcludeFromDescription();
        }

        return app;
    }
}

public sealed record OrderStatusResponse(string Status);
