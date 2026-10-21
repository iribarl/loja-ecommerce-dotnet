using Loja.Api.Catalog;
using Loja.Api.Payments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Loja.Api;

public static class ApiEndpoints
{
    public const string Prefix = "/api";

    /// <summary>
    /// Registers the services the HTTP API needs.
    /// </summary>
    public static IServiceCollection AddLojaApi(this IServiceCollection services)
    {
        // Must be called from this assembly: the validation source generator only
        // discovers endpoints and request types in the project that calls AddValidation().
        services.AddValidation();

        return services;
    }

    /// <summary>
    /// Maps every endpoint group of the HTTP API under <see cref="Prefix"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapLojaApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup(Prefix);

        api.MapCatalogEndpoints();
        api.MapPaymentWebhookEndpoints();

        return app;
    }
}
