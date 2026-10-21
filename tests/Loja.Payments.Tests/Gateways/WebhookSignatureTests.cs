using Loja.Payments.Infrastructure.Gateways;

namespace Loja.Payments.Tests.Gateways;

public class WebhookSignatureTests
{
    private const string Secret = "test-secret";
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SignedByTheProvider_IsValid()
    {
        var header = WebhookSignature.Sign(Secret, "pay_123", "req-1", Now.ToUnixTimeSeconds());

        WebhookSignature.IsValid(Secret, header, "pay_123", "req-1", Now).ShouldBeTrue();
    }

    [Theory]
    [InlineData("other-secret", "pay_123", "req-1")]  // someone without the secret
    [InlineData(Secret, "pay_999", "req-1")]           // payment id swapped
    [InlineData(Secret, "pay_123", "req-2")]           // request id swapped
    public void AnyChange_MakesItInvalid(string secret, string dataId, string requestId)
    {
        var header = WebhookSignature.Sign(secret, "pay_123", "req-1", Now.ToUnixTimeSeconds());

        WebhookSignature.IsValid(Secret, header, dataId, requestId, Now).ShouldBeFalse();
    }

    [Fact]
    public void OldSignature_IsRefused_ToStopReplays()
    {
        var header = WebhookSignature.Sign(Secret, "pay_123", "req-1", Now.AddMinutes(-10).ToUnixTimeSeconds());

        WebhookSignature.IsValid(Secret, header, "pay_123", "req-1", Now).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1=abc")]
    [InlineData("ts=abc,v1=00")]
    [InlineData("ts=1729425600,v1=not-hex")]
    public void MalformedHeader_IsInvalid(string? header)
    {
        WebhookSignature.IsValid(Secret, header, "pay_123", "req-1", Now).ShouldBeFalse();
    }
}

public class PixCodeTests
{
    [Fact]
    public void Crc16_MatchesTheCentralBankExample()
    {
        // Example from the BR Code manual (Banco Central do Brasil): the payload up to "6304" has CRC 1D3D.
        const string payload =
            "00020126580014br.gov.bcb.pix0136123e4567-e12b-12d1-a456-426655440000" +
            "5204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***6304";

        PixCode.Crc16(payload).ToString("X4", System.Globalization.CultureInfo.InvariantCulture).ShouldBe("1D3D");
    }

    [Fact]
    public void Build_WritesTheAmountAndEndsWithItsChecksum()
    {
        var code = PixCode.Build("pagamentos@loja-demo.example", 342.60m, "LOJA DEMO", "SAO PAULO", "tx123");

        code.ShouldStartWith("000201");
        code.ShouldContain("5406342.60");
        var checksum = PixCode.Crc16(code[..^4]).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        code[^4..].ShouldBe(checksum);
    }
}
