using System.Net.Http.Json;
using System.Text.Json;
using Loja.SharedKernel;
using Loja.Shipping.Contracts;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Loja.Shipping.Infrastructure;

/// <summary>
/// Address lookup with ViaCEP (https://viacep.com.br), a free public service for Brazilian CEPs.
/// </summary>
/// <remarks>
/// Only the CEP is sent, never data about the customer. Results are cached for a day: the address of
/// a CEP almost never changes, and the cache spares the service and makes repeated lookups instant.
/// Retries and timeouts come from the resilience defaults (ServiceDefaults) plus a short HttpClient timeout.
/// </remarks>
internal sealed partial class ViaCepAddressLookup(
    HttpClient http,
    HybridCache cache,
    ILogger<ViaCepAddressLookup> logger) : IAddressLookup
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromDays(1),
        LocalCacheExpiration = TimeSpan.FromHours(1),
    };

    public async Task<AddressLookupResult> LookupAsync(string cep, CancellationToken cancellationToken)
    {
        if (Cep.Create(cep) is not { IsSuccess: true } valid)
        {
            return AddressLookupResult.InvalidCep;
        }

        try
        {
            // Found and not-found answers are cached; failures are not (the factory throws), so the
            // next attempt asks the service again.
            var cached = await cache.GetOrCreateAsync(
                $"shipping:cep:{valid.Value.Digits}",
                (http, valid.Value),
                static (state, ct) => FetchAsync(state.http, state.Item2, ct),
                CacheOptions,
                cancellationToken: cancellationToken);

            return cached.Address is null ? AddressLookupResult.NotFound : AddressLookupResult.Found(cached.Address);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                   && !cancellationToken.IsCancellationRequested)
        {
            LookupFailed(logger, ex, valid.Value.ToString());
            return AddressLookupResult.Unavailable;
        }
    }

    private static async ValueTask<CachedLookup> FetchAsync(HttpClient http, Cep cep, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync($"ws/{cep.Digits}/json/", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);

        // A CEP that does not exist answers 200 with {"erro": true} (older versions: "erro": "true").
        if (json.TryGetProperty("erro", out _))
        {
            return new CachedLookup(null);
        }

        return new CachedLookup(new AddressInfo(
            cep.ToString(),
            Text(json, "logradouro"),
            Text(json, "bairro"),
            Text(json, "localidade"),
            Text(json, "uf")));
    }

    private static string Text(JsonElement json, string property) =>
        json.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Trim()
            : string.Empty;

    /// <summary>What goes into the cache: an address, or null for a CEP that does not exist.</summary>
    internal sealed record CachedLookup(AddressInfo? Address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ViaCEP lookup for {Cep} failed; the shopper will type the address.")]
    private static partial void LookupFailed(ILogger logger, Exception exception, string cep);
}
