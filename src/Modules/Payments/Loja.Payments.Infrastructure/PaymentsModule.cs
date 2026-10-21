using Loja.Outbox;
using Loja.Payments.Application;
using Loja.Payments.Application.CreatePixCharge;
using Loja.Payments.Application.ExpirePixCharges;
using Loja.Payments.Application.HandleWebhook;
using Loja.Payments.Contracts;
using Loja.Payments.Domain;
using Loja.Payments.Infrastructure.Gateways;
using Loja.Payments.Infrastructure.Persistence;
using Loja.Shared.Infrastructure.Persistence;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loja.Payments.Infrastructure;

/// <summary>
/// Entry point of the Payments module.
/// </summary>
/// <remarks>Needs <c>IOrdersApi</c>, registered by the Orders module.</remarks>
public static class PaymentsModule
{
    private static readonly OutboxInterceptor Outbox = new(new PaymentsIntegrationEventMapper());

    public static IHostApplicationBuilder AddPaymentsModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<PaymentsDbContext>(options => options
            .UseNpgsql(builder.Configuration.GetConnectionString(LojaDatabase.ConnectionStringName), PaymentsDbContext.ConfigureNpgsql)
            .AddInterceptors(Outbox));
        builder.EnrichNpgsqlDbContext<PaymentsDbContext>();
        builder.Services.AddOutboxProcessor<PaymentsDbContext>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

        AddGateways(builder);

        builder.Services.AddScoped<CreatePixChargeHandler>();
        builder.Services.AddScoped<GetOrderPaymentHandler>();
        builder.Services.AddScoped<HandlePaymentWebhookHandler>();
        builder.Services.AddScoped<ExpirePixChargesHandler>();
        builder.Services.AddHostedService<PixExpirySweeper>();

        return builder;
    }

    /// <summary>Applies pending migrations. Development only.</summary>
    public static async Task InitializePaymentsDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PaymentsModule));

        await db.MigrateWithRetryAsync(logger, cancellationToken);
    }

    private static void AddGateways(IHostApplicationBuilder builder)
    {
        var defaultGateway = builder.Configuration["Payments:Gateway"] ?? FakePixGateway.GatewayName;

        // The fake gateway exists only outside production: a store that "accepts" payments without
        // money must never be published by mistake.
        if (builder.Environment.IsProduction())
        {
            if (defaultGateway == FakePixGateway.GatewayName)
            {
                throw new InvalidOperationException("The fake payment gateway cannot be used in production. Set Payments:Gateway.");
            }
        }
        else
        {
            var secret = builder.Configuration["Payments:Fake:WebhookSecret"] ?? "dev-only-fake-webhook-secret";
            builder.Services.AddSingleton(sp => new FakePixGateway(secret, sp.GetRequiredService<TimeProvider>()));
            builder.Services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<FakePixGateway>());
        }

        builder.Services.AddSingleton<IPaymentGatewayResolver>(sp =>
            new PaymentGatewayResolver(sp.GetServices<IPaymentGateway>(), defaultGateway));
    }
}

/// <summary>Which payment events other modules hear about.</summary>
internal sealed class PaymentsIntegrationEventMapper : IIntegrationEventMapper
{
    public IntegrationEvent? Map(IDomainEvent domainEvent) => domainEvent switch
    {
        PixChargeCreated created => new PixChargeCreatedIntegrationEvent(created.OrderId, created.PaymentId.Value, created.ExpiresAt),
        PaymentConfirmed confirmed => new PaymentConfirmedIntegrationEvent(
            confirmed.OrderId, confirmed.PaymentId.Value, confirmed.Amount.Amount, confirmed.Amount.Currency),
        PaymentExpired expired => new PaymentExpiredIntegrationEvent(expired.OrderId, expired.PaymentId.Value),
        _ => null,
    };
}
