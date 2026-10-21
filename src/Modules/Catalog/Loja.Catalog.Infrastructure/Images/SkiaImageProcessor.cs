using Loja.Catalog.Application.Products.Images;
using Loja.Catalog.Domain;
using Loja.SharedKernel;
using SkiaSharp;

namespace Loja.Catalog.Infrastructure.Images;

internal sealed class SkiaImageProcessor : IImageProcessor
{
    /// <summary>Largest side accepted. Checked from the header, before decoding.</summary>
    public const int MaxDimension = 8000;

    private const int WebpQuality = 80;

    private static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    public Task<Result<IReadOnlyList<ImageRendition>>> ProcessAsync(Stream content, CancellationToken cancellationToken)
    {
        // The codec reads only the header: format, size and orientation, without decoding the pixels.
        using var codec = SKCodec.Create(content);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
        {
            return Failure(CatalogErrors.UnsupportedImage);
        }

        // A small file can declare a huge picture (a "decompression bomb": 1 KB that needs
        // gigabytes once decoded), so the size is checked before decoding.
        if (codec.Info.Width > MaxDimension || codec.Info.Height > MaxDimension)
        {
            return Failure(CatalogErrors.ImageResolutionTooLarge);
        }

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return Failure(CatalogErrors.UnsupportedImage);
        }

        // Phones store the picture sideways plus an "orientation" flag: apply it. The metadata
        // (EXIF, which can carry the GPS position) is not copied: Skia encodes pixels only.
        using var upright = Orient(decoded, codec.EncodedOrigin);

        var renditions = new List<ImageRendition>(ImageSizes.All.Count);
        foreach (var size in ImageSizes.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            renditions.Add(new ImageRendition(size, Encode(upright, size)));
        }

        return Task.FromResult(Result.Success<IReadOnlyList<ImageRendition>>(renditions));
    }

    // Fits inside a size × size square keeping its proportions; never enlarged.
    private static byte[] Encode(SKBitmap source, int size)
    {
        var scale = Math.Min(1d, (double)size / Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = scale < 1 ? source.Resize(new SKImageInfo(width, height), Sampling) : null;
        using var image = SKImage.FromBitmap(resized ?? source);
        using var data = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
        return data.ToArray();
    }

    // The 8 EXIF orientations as the transform that puts the stored pixels upright. With the
    // source w × h, each maps (x, y) to its place on the target (w × h, or h × w when turned).
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width, h = source.Height;
        SKMatrix? transform = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // mirrored
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // 180°
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // flipped
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transposed
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // 90° clockwise
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transversed
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // 90° counter-clockwise
            _ => null,
        };

        if (transform is not { } matrix)
        {
            return source.Copy();
        }

        var turned = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var upright = new SKBitmap(turned ? source.Height : source.Width, turned ? source.Width : source.Height, source.ColorType, source.AlphaType);
        using var canvas = new SKCanvas(upright);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0);
        return upright;
    }

    private static Task<Result<IReadOnlyList<ImageRendition>>> Failure(Error error) =>
        Task.FromResult(Result.Failure<IReadOnlyList<ImageRendition>>(error));
}
