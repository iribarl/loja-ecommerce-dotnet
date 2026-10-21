using System.Text;
using Loja.Catalog.Application.Products.Images;
using Loja.Catalog.Domain;
using Loja.Catalog.Infrastructure.Images;
using SkiaSharp;

namespace Loja.IntegrationTests.Catalog;

/// <summary>
/// The real image processor (SkiaSharp) on pictures made here. No database involved.
/// </summary>
public sealed class ImageProcessorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly SkiaImageProcessor _processor = new();

    [Fact]
    public async Task Landscape_GetsEverySize_KeepingItsProportions()
    {
        var renditions = (await Process(TestImages.Png(2000, 1000))).Value;

        renditions.Select(r => (r.Size, Dimensions(r))).ShouldBe(
        [
            (ImageSizes.Thumbnail, (160, 80)),
            (ImageSizes.Card, (480, 240)),
            (ImageSizes.Large, (1200, 600)),
        ]);
        renditions.ShouldAllBe(r => Format(r) == SKEncodedImageFormat.Webp);
    }

    [Fact]
    public async Task SmallPicture_IsNeverEnlarged()
    {
        var renditions = (await Process(TestImages.Png(300, 100))).Value;

        Dimensions(renditions.Single(r => r.Size == ImageSizes.Thumbnail)).ShouldBe((160, 53));
        Dimensions(renditions.Single(r => r.Size == ImageSizes.Card)).ShouldBe((300, 100));
        Dimensions(renditions.Single(r => r.Size == ImageSizes.Large)).ShouldBe((300, 100));
    }

    [Fact]
    public async Task PhonePhotoStoredSideways_IsTurnedUpright_AndLosesItsMetadata()
    {
        // As a phone saves a portrait photo: landscape pixels plus "rotate 90°" in the EXIF data.
        var photo = TestImages.WithExifOrientation(TestImages.Jpeg(200, 100), orientation: 6);
        Contains(photo, "Exif").ShouldBeTrue();

        var large = (await Process(photo)).Value.Single(r => r.Size == ImageSizes.Large);

        Dimensions(large).ShouldBe((100, 200));
        // Turned the right way: the orange top-left corner of the stored pixels ends up top-right.
        using (var upright = SKBitmap.Decode(large.Content))
        {
            IsOrange(upright.GetPixel(75, 25)).ShouldBeTrue();
            IsOrange(upright.GetPixel(25, 25)).ShouldBeFalse();
            IsOrange(upright.GetPixel(75, 175)).ShouldBeFalse();
        }

        Contains(large.Content, "Exif").ShouldBeFalse();
        Contains(large.Content, "EXIF").ShouldBeFalse();
    }

    [Fact]
    public async Task HugeResolution_IsRefusedBeforeDecoding()
    {
        var result = await Process(TestImages.Png(SkiaImageProcessor.MaxDimension + 1, 1));

        result.Error.ShouldBe(CatalogErrors.ImageResolutionTooLarge);
    }

    [Theory]
    [InlineData("474946383961010001008000000000000000ffffff21f90401000000002c00000000010001000002024401003b")] // a 1×1 GIF
    [InlineData("89504e470d0a1a0a0000000d49484452deadbeef")] // a PNG signature with garbage after it
    public async Task OtherFormatsOrBrokenFiles_AreRefused(string hex)
    {
        var result = await Process(Convert.FromHexString(hex));

        result.Error.ShouldBe(CatalogErrors.UnsupportedImage);
    }

    private async Task<Loja.SharedKernel.Result<IReadOnlyList<ImageRendition>>> Process(byte[] bytes) =>
        await _processor.ProcessAsync(new MemoryStream(bytes), Ct);

    private static (int Width, int Height) Dimensions(ImageRendition rendition)
    {
        using var codec = SKCodec.Create(new MemoryStream(rendition.Content));
        return (codec.Info.Width, codec.Info.Height);
    }

    private static SKEncodedImageFormat Format(ImageRendition rendition)
    {
        using var codec = SKCodec.Create(new MemoryStream(rendition.Content));
        return codec.EncodedFormat;
    }

    private static bool IsOrange(SKColor color) => color.Red > 200 && color.Green is > 120 and < 200 && color.Blue < 80;

    private static bool Contains(byte[] bytes, string text) =>
        bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;
}

/// <summary>Pictures made in memory for the tests.</summary>
internal static class TestImages
{
    public static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    /// <summary>
    /// Inserts a minimal EXIF block (APP1) right after the JPEG's start marker, holding only the
    /// orientation tag (0x0112), as cameras and phones write it.
    /// </summary>
    public static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] tiff =
        [
            .. "II"u8, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00, // little-endian TIFF header, first IFD at offset 8
            0x01, 0x00,                                     // one entry
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, // tag 0x0112 (orientation), SHORT, count 1
            (byte)orientation, 0x00, 0x00, 0x00,            // its value
            0x00, 0x00, 0x00, 0x00,                         // no next IFD
        ];
        byte[] payload = [.. "Exif\0\0"u8, .. tiff];
        var length = payload.Length + 2;

        return [jpeg[0], jpeg[1], 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload, .. jpeg.AsSpan(2)];
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SteelBlue);
            using var paint = new SKPaint { Color = SKColors.Orange };
            canvas.DrawRect(0, 0, width / 2f, height / 2f, paint); // top-left corner marks the orientation
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
