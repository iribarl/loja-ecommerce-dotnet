using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Loja.Payments.Infrastructure.Gateways;

/// <summary>
/// Webhook signatures in the Mercado Pago scheme, so the fake gateway exercises the same validation
/// the real one needs. Header <c>x-signature: ts=1729425600,v1=&lt;hex&gt;</c>, where v1 is the
/// HMAC-SHA256 of <c>id:{data.id};request-id:{x-request-id};ts:{ts};</c> with the shared secret.
/// </summary>
internal static class WebhookSignature
{
    /// <summary>An old signed request replayed later is refused.</summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public static string Sign(string secret, string dataId, string requestId, long timestamp)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(Manifest(dataId, requestId, timestamp)));
        return $"ts={timestamp.ToString(CultureInfo.InvariantCulture)},v1={Convert.ToHexStringLower(hash)}";
    }

    public static bool IsValid(string secret, string? header, string dataId, string requestId, DateTimeOffset now)
    {
        if (!TryParse(header, out var timestamp, out var signature))
        {
            return false;
        }

        if ((now - DateTimeOffset.FromUnixTimeSeconds(timestamp)).Duration() > Tolerance)
        {
            return false;
        }

        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(Manifest(dataId, requestId, timestamp)));

        // Constant-time comparison: comparing byte by byte and stopping at the first difference would
        // let an attacker learn the right signature from response times.
        return signature.Length == expected.Length && CryptographicOperations.FixedTimeEquals(signature, expected);
    }

    private static string Manifest(string dataId, string requestId, long timestamp) =>
        string.Create(CultureInfo.InvariantCulture, $"id:{dataId.ToLowerInvariant()};request-id:{requestId};ts:{timestamp};");

    private static bool TryParse(string? header, out long timestamp, out byte[] signature)
    {
        timestamp = 0;
        signature = [];
        string? ts = null;
        string? v1 = null;

        foreach (var part in (header ?? string.Empty).Split(',', StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2);
            if (pair is ["ts", var value])
            {
                ts = value;
            }
            else if (pair is ["v1", var hex])
            {
                v1 = hex;
            }
        }

        if (!long.TryParse(ts, NumberStyles.None, CultureInfo.InvariantCulture, out timestamp) || v1 is null)
        {
            return false;
        }

        try
        {
            signature = Convert.FromHexString(v1);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
