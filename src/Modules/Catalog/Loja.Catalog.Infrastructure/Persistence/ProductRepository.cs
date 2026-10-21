using Loja.Catalog.Application.Products;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Loja.Catalog.Infrastructure.Persistence;

internal sealed class ProductRepository(CatalogDbContext db) : IProductRepository
{
    public Task<Product?> GetAsync(ProductId id, CancellationToken cancellationToken) =>
        db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<bool> SlugExistsAsync(Slug slug, CancellationToken cancellationToken) =>
        db.Products.AnyAsync(p => p.Slug == slug, cancellationToken);

    public Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken) =>
        db.Set<ProductVariant>().AnyAsync(v => v.Sku == sku, cancellationToken);

    public Task<bool> CategoryExistsAsync(CategoryId id, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(c => c.Id == id, cancellationToken);

    public void Add(Product product) => db.Products.Add(product);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
