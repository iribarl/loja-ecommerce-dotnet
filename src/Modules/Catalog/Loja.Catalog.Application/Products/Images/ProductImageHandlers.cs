using Loja.Catalog.Application.Products.ManageProducts;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Products;
using Loja.SharedKernel;

namespace Loja.Catalog.Application.Products.Images;

/// <summary>
/// Recognizes the image formats the store accepts by their first bytes ("magic numbers"), not by
/// the file name or the Content-Type the browser sends: both are chosen by the uploader.
/// SVG is not accepted: it is a document that can carry scripts.
/// </summary>
/// <remarks>A cheap first gate; the <see cref="IImageProcessor"/> then decodes the file for real.</remarks>
public static class ImageFormat
{
    public const int HeaderLength = 12;

    /// <returns>".jpg", ".png" or ".webp"; null for anything else.</returns>
    public static string? Detect(ReadOnlySpan<byte> header) => header switch
    {
        [0xFF, 0xD8, 0xFF, ..] => ".jpg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => ".png",
        // "RIFF" + 4 bytes of size + "WEBP"
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => ".webp",
        _ => null,
    };
}

/// <summary>
/// The upload is a plain form post that redirects back to the product page with the error code
/// in the URL; this turns the code back into its message. Unknown codes get a generic message,
/// so a crafted link cannot put arbitrary text on the page.
/// </summary>
public static class ProductImageMessages
{
    private static readonly Dictionary<string, string> Messages = new[]
    {
        CatalogErrors.UnsupportedImage, CatalogErrors.ImageTooLarge, CatalogErrors.ImageResolutionTooLarge,
        CatalogErrors.TooManyImages, CatalogErrors.ImageNotFound, CatalogErrors.InvalidAttribute, CatalogErrors.ProductNotFound,
    }.ToDictionary(error => error.Code, error => error.Message, StringComparer.Ordinal);

    public static string For(string? code) =>
        code is not null && Messages.TryGetValue(code, out var message) ? message : "Não foi possível salvar a imagem.";
}

public sealed class AddProductImageHandler(
    IProductRepository products,
    IImageProcessor processor,
    IImageStorage storage,
    ICatalogCacheInvalidator cache)
{
    public const int MaxBytes = 5 * 1024 * 1024;

    /// <param name="appliesTo">Optional "Name: value" lines (e.g. "Cor: Preto") when the image shows some variants only.</param>
    public async Task<Result> HandleAsync(Guid productId, Stream content, string? appliesTo, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(new ProductId(productId), cancellationToken);
        if (product is null)
        {
            return CatalogErrors.ProductNotFound;
        }

        var attributes = AttributesText.Parse(appliesTo);
        if (attributes.IsFailure)
        {
            return attributes.Error;
        }

        // Read at most one byte more than allowed: the declared length is the uploader's word, this is a fact.
        using var upload = new MemoryStream();
        await CopyAtMostAsync(content, upload, MaxBytes + 1, cancellationToken);
        if (upload.Length > MaxBytes)
        {
            return CatalogErrors.ImageTooLarge;
        }

        if (ImageFormat.Detect(upload.GetBuffer().AsSpan(0, (int)Math.Min(upload.Length, ImageFormat.HeaderLength))) is null)
        {
            return CatalogErrors.UnsupportedImage;
        }

        upload.Position = 0;
        var renditions = await processor.ProcessAsync(upload, cancellationToken);
        if (renditions.IsFailure)
        {
            return renditions.Error;
        }

        var url = await storage.SaveAsync(renditions.Value, cancellationToken);

        var added = product.AddImage(url, attributes.Value);
        if (added.IsFailure)
        {
            await storage.DeleteAsync(url, cancellationToken); // no orphan files
            return added;
        }

        await products.SaveChangesAsync(cancellationToken);
        await cache.InvalidateAsync(cancellationToken);
        return Result.Success();
    }

    private static async Task CopyAtMostAsync(Stream source, Stream destination, int limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while (total < limit && (read = await source.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, limit - total)), cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            total += read;
        }
    }
}

public sealed class RemoveProductImageHandler(IProductRepository products, IImageStorage storage, ICatalogCacheInvalidator cache)
{
    public async Task<Result> HandleAsync(Guid productId, string url, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(new ProductId(productId), cancellationToken);
        if (product is null)
        {
            return CatalogErrors.ProductNotFound;
        }

        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var imageUrl))
        {
            return CatalogErrors.ImageNotFound;
        }

        var removed = product.RemoveImage(imageUrl);
        if (removed.IsFailure)
        {
            return removed;
        }

        // The product first: if deleting the files fails, unused files are harmless; a product
        // pointing at missing files is not.
        await products.SaveChangesAsync(cancellationToken);
        await storage.DeleteAsync(imageUrl, cancellationToken);
        await cache.InvalidateAsync(cancellationToken);
        return Result.Success();
    }
}
