using Loja.Catalog.Application.Products;
using Loja.Catalog.Application.Products.Images;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;

namespace Loja.Catalog.Tests.Application;

internal sealed class FakeProductRepository(CategoryId category) : IProductRepository
{
    public List<Product> Saved { get; } = [];

    public HashSet<string> SkusElsewhere { get; } = [];

    public int SaveCount { get; private set; }

    public Task<Product?> GetAsync(ProductId id, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.Find(p => p.Id == id));

    public Task<bool> SlugExistsAsync(Slug slug, CancellationToken cancellationToken) =>
        Task.FromResult(Saved.Exists(p => p.Slug == slug));

    public Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken) =>
        Task.FromResult(SkusElsewhere.Contains(sku.Value) || Saved.Exists(p => p.Variants.Any(v => v.Sku == sku)));

    public Task<bool> CategoryExistsAsync(CategoryId id, CancellationToken cancellationToken) =>
        Task.FromResult(id == category);

    public void Add(Product product) => Saved.Add(product);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCache : ICatalogCacheInvalidator
{
    public int Invalidations { get; private set; }

    public Task InvalidateAsync(CancellationToken cancellationToken)
    {
        Invalidations++;
        return Task.CompletedTask;
    }
}

/// <summary>Keeps "saved" images in memory.</summary>
internal sealed class FakeImageStorage : IImageStorage
{
    public Dictionary<Uri, byte[]> Files { get; } = [];

    public List<Uri> Deleted { get; } = [];

    public async Task<Uri> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        var url = new Uri($"/uploads/produtos/{Guid.NewGuid():N}{extension}", UriKind.Relative);
        Files[url] = copy.ToArray();
        return url;
    }

    public Task DeleteAsync(Uri url, CancellationToken cancellationToken)
    {
        Files.Remove(url);
        Deleted.Add(url);
        return Task.CompletedTask;
    }
}
