using System.Text.RegularExpressions;
using Loja.Catalog.Application.Products.Images;

namespace Loja.Catalog.Infrastructure.Images;

/// <summary>
/// Product images on the server's disk, served by the host under <see cref="CatalogModule.ImagesRequestPath"/>:
/// "{id}-160.webp", "{id}-480.webp" and "{id}-1200.webp" per image.
/// Fine for development and a single-instance demo; production swaps it for a blob storage (phase 6).
/// </summary>
/// <remarks>
/// File names are chosen here (a new Guid), never taken from the upload: a name such as
/// "../../appsettings.json" can then never reach the disk.
/// </remarks>
internal sealed partial class LocalImageStorage(string rootPath) : IImageStorage
{
    public async Task<Uri> SaveAsync(IReadOnlyList<ImageRendition> renditions, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(rootPath);
        var id = Guid.CreateVersion7().ToString("N");

        foreach (var rendition in renditions)
        {
            await File.WriteAllBytesAsync(Path.Combine(rootPath, $"{id}-{rendition.Size}.webp"), rendition.Content, cancellationToken);
        }

        return new Uri($"{CatalogModule.ImagesRequestPath}/{id}-{renditions.Max(r => r.Size)}.webp", UriKind.Relative);
    }

    public Task DeleteAsync(Uri url, CancellationToken cancellationToken)
    {
        var prefix = CatalogModule.ImagesRequestPath + "/";
        var path = url.OriginalString;

        // Only files this storage named: seed images and external links are not ours to delete.
        if (url.IsAbsoluteUri || !path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var match = OwnFileName().Match(path[prefix.Length..]);
        if (match.Success)
        {
            // Every rendition of the image (and the single file of an upload from before the sizes existed).
            foreach (var file in Directory.EnumerateFiles(rootPath, $"{match.Groups["id"].Value}*"))
            {
                File.Delete(file);
            }
        }

        return Task.CompletedTask;
    }

    [GeneratedRegex(@"^(?<id>[0-9a-f]{32})(-\d+)?\.(jpg|png|webp)$")]
    private static partial Regex OwnFileName();
}
