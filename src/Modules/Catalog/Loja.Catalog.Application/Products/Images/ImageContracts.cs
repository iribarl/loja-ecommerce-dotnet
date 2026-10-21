using System.Text.RegularExpressions;
using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products.Images;

/// <summary>
/// Turns an uploaded file into the sizes the store shows: decoded for real, turned upright,
/// stripped of metadata (a phone photo can carry its GPS position) and re-encoded as WebP.
/// </summary>
public interface IImageProcessor
{
    /// <returns>One rendition per <see cref="ImageSizes.All"/> size; an error when it is not a usable image.</returns>
    Task<Result<IReadOnlyList<ImageRendition>>> ProcessAsync(Stream content, CancellationToken cancellationToken);
}

/// <param name="Size">The square box (in pixels) the picture fits in, e.g. 480.</param>
/// <param name="Content">The WebP file.</param>
public sealed record ImageRendition(int Size, byte[] Content);

/// <summary>Where uploaded product images live (local disk today, a blob storage in production).</summary>
public interface IImageStorage
{
    /// <summary>Stores the renditions of one image under a name the storage chooses.</summary>
    /// <returns>The URL of the largest one, which is what the product keeps.</returns>
    Task<Uri> SaveAsync(IReadOnlyList<ImageRendition> renditions, CancellationToken cancellationToken);

    /// <summary>Deletes an image (all its renditions) this storage saved; any other URL is left alone.</summary>
    Task DeleteAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>
/// The sizes generated for each uploaded image and how their URLs relate:
/// "/uploads/produtos/{id}-1200.webp" also exists as "-480" and "-160".
/// </summary>
public static partial class ImageSizes
{
    public const int Thumbnail = 160;
    public const int Card = 480;
    public const int Large = 1200;

    public static readonly IReadOnlyList<int> All = [Thumbnail, Card, Large];

    /// <summary>The URL of <paramref name="url"/> in another size; the URL itself for images without sizes (seed SVGs, old uploads).</summary>
    public static Uri For(Uri url, int size) =>
        Sized().Match(url.OriginalString) is { Success: true } match
            ? new Uri($"{match.Groups["base"].Value}-{size}.webp", UriKind.RelativeOrAbsolute)
            : url;

    /// <summary>A srcset listing every size ("… 160w, … 480w, … 1200w"); null for images without sizes.</summary>
    /// <remarks>The renditions fit a square box, so their width is at most the size: the browser may pick one a bit larger than needed, never smaller.</remarks>
    public static string? SrcSet(Uri url) =>
        Sized().IsMatch(url.OriginalString)
            ? string.Join(", ", All.Select(size => $"{For(url, size)} {size}w"))
            : null;

    [GeneratedRegex(@"^(?<base>.+)-(160|480|1200)\.webp$")]
    private static partial Regex Sized();
}
