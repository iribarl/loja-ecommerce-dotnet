using System.Text.RegularExpressions;
using Loja.Catalog.Application.Products.Images;

namespace Loja.Catalog.Infrastructure.Images;

/// <summary>
/// Product images on the server's disk, served by the host under <see cref="CatalogModule.ImagesRequestPath"/>.
/// Fine for development and a single-instance demo; production swaps it for a blob storage (phase 6).
/// </summary>
/// <remarks>
/// File names are chosen here (a new Guid), never taken from the upload: a name such as
/// "../../appsettings.json" can then never reach the disk.
/// </remarks>
internal sealed partial class LocalImageStorage(string rootPath) : IImageStorage
{
    public async Task<Uri> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        if (!AllowedExtension().IsMatch(extension))
        {
            throw new ArgumentException($"Unexpected image extension '{extension}'.", nameof(extension));
        }

        Directory.CreateDirectory(rootPath);
        var fileName = $"{Guid.CreateVersion7():N}{extension}";

        await using (var file = new FileStream(Path.Combine(rootPath, fileName), FileMode.CreateNew, FileAccess.Write))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        return new Uri($"{CatalogModule.ImagesRequestPath}/{fileName}", UriKind.Relative);
    }

    public Task DeleteAsync(Uri url, CancellationToken cancellationToken)
    {
        var prefix = CatalogModule.ImagesRequestPath + "/";
        var path = url.OriginalString;

        // Only files this storage named: seed images and external links are not ours to delete.
        if (!url.IsAbsoluteUri && path.StartsWith(prefix, StringComparison.Ordinal) && OwnFileName().IsMatch(path[prefix.Length..]))
        {
            File.Delete(Path.Combine(rootPath, path[prefix.Length..]));
        }

        return Task.CompletedTask;
    }

    [GeneratedRegex(@"^\.(jpg|png|webp)$")]
    private static partial Regex AllowedExtension();

    [GeneratedRegex(@"^[0-9a-f]{32}\.(jpg|png|webp)$")]
    private static partial Regex OwnFileName();
}
