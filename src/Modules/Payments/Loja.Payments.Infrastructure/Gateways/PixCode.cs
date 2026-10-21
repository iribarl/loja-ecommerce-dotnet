using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Loja.Payments.Infrastructure.Gateways;

/// <summary>
/// Builds a "Pix copia e cola" in the Central Bank's BR Code format (EMV): fields written as
/// ID + length + value, closed by a CRC16 checksum. Used by the fake gateway so the demo looks real;
/// the real gateway returns its own code.
/// </summary>
internal static class PixCode
{
    public static string Build(string pixKey, decimal amount, string merchantName, string merchantCity, string txId)
    {
        var payload = new StringBuilder()
            .Append(Field("00", "01"))                                      // payload format
            .Append(Field("26", Field("00", "br.gov.bcb.pix") + Field("01", pixKey)))
            .Append(Field("52", "0000"))                                    // merchant category
            .Append(Field("53", "986"))                                     // currency: BRL
            .Append(Field("54", amount.ToString("0.00", CultureInfo.InvariantCulture)))
            .Append(Field("58", "BR"))
            .Append(Field("59", merchantName))
            .Append(Field("60", merchantCity))
            .Append(Field("62", Field("05", txId)))
            .Append("6304")                                                 // CRC field, value below
            .ToString();

        return payload + Crc16(payload).ToString("X4", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A QR-looking SVG for the fake gateway (no QR library just for a simulation): a grid derived
    /// from the code with the three corner markers. It is not scannable; the real gateway sends a real QR.
    /// </summary>
    public static string PlaceholderQrDataUri(string code)
    {
        const int cells = 25;
        var bits = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        var svg = new StringBuilder($"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {cells} {cells}" shape-rendering="crispEdges"><rect width="{cells}" height="{cells}" fill="#fff"/>""");

        for (var y = 0; y < cells; y++)
        {
            for (var x = 0; x < cells; x++)
            {
                var index = (y * cells) + x;
                if (!IsInFinder(x, y, cells) && (bits[index % bits.Length] >> (index % 8) & 1) == 1)
                {
                    svg.Append(CultureInfo.InvariantCulture, $"""<rect x="{x}" y="{y}" width="1" height="1"/>""");
                }
            }
        }

        foreach (var (fx, fy) in new[] { (0, 0), (cells - 7, 0), (0, cells - 7) })
        {
            svg.Append(CultureInfo.InvariantCulture, $"""<rect x="{fx}" y="{fy}" width="7" height="7"/><rect x="{fx + 1}" y="{fy + 1}" width="5" height="5" fill="#fff"/><rect x="{fx + 2}" y="{fy + 2}" width="3" height="3"/>""");
        }

        svg.Append("</svg>");
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.ToString()));
    }

    private static string Field(string id, string value) =>
        id + value.Length.ToString("00", CultureInfo.InvariantCulture) + value;

    // CRC16-CCITT (polynomial 0x1021, initial value 0xFFFF), as required by the BR Code spec.
    internal static ushort Crc16(string data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(data))
        {
            crc ^= (ushort)(b << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
        }

        return crc;
    }

    private static bool IsInFinder(int x, int y, int cells) =>
        (x < 8 && y < 8) || (x >= cells - 8 && y < 8) || (x < 8 && y >= cells - 8);
}
