using Loja.Catalog.Domain.Categories;
using Loja.SharedKernel;

namespace Loja.Catalog.Domain.Products;

public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.CreateVersion7());
}

/// <summary>
/// Aggregate root of the catalog. Variants, prices and stock change only through its methods.
/// A product starts as a draft and is visible in the store once published.
/// </summary>
public sealed class Product : Entity<ProductId>
{
    private readonly List<ProductVariant> _variants = [];
    private readonly List<ProductImage> _images = [];

    private Product(ProductId id, string name, Slug slug, string description, CategoryId categoryId)
        : base(id)
    {
        Name = name;
        Slug = slug;
        Description = description;
        CategoryId = categoryId;
    }

    public string Name { get; private set; }

    public Slug Slug { get; private set; }

    public string Description { get; private set; }

    public CategoryId CategoryId { get; private set; }

    public bool IsPublished { get; private set; }

    public IReadOnlyList<ProductVariant> Variants => _variants.AsReadOnly();

    /// <summary>In display order.</summary>
    public IReadOnlyList<ProductImage> Images => _images.AsReadOnly();

    /// <param name="slug">The product's URL; by default made from the name.</param>
    public static Result<Product> Create(string name, string description, CategoryId categoryId, Slug? slug = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CatalogErrors.NameRequired;
        }

        if (slug is null)
        {
            var fromName = Slug.From(name);
            if (fromName.IsFailure)
            {
                return fromName.Error;
            }

            slug = fromName.Value;
        }

        return new Product(ProductId.New(), name.Trim(), slug, description?.Trim() ?? string.Empty, categoryId);
    }

    /// <summary>
    /// Renames, re-describes or moves the product to another category. The slug stays: the
    /// product's URL must not change, or links and bookmarks to it would break.
    /// </summary>
    public Result UpdateDetails(string name, string description, CategoryId categoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CatalogErrors.NameRequired;
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        CategoryId = categoryId;
        return Result.Success();
    }

    public Result ChangeAttributes(Sku sku, VariantAttributes attributes)
    {
        if (_variants.Exists(v => v.Sku != sku && v.Attributes.Equals(attributes)))
        {
            return CatalogErrors.DuplicateAttributes;
        }

        return WithVariant(sku, variant => variant.ChangeAttributes(attributes));
    }

    public Result<ProductVariant> AddVariant(Sku sku, VariantAttributes attributes, Money price, int stock)
    {
        if (price.Amount <= 0)
        {
            return CatalogErrors.InvalidPrice;
        }

        if (stock < 0)
        {
            return CatalogErrors.NegativeStock;
        }

        if (_variants.Exists(v => v.Sku == sku))
        {
            return CatalogErrors.DuplicateSku;
        }

        if (_variants.Exists(v => v.Attributes.Equals(attributes)))
        {
            return CatalogErrors.DuplicateAttributes;
        }

        var variant = new ProductVariant(ProductVariantId.New(), _variants.Count, sku, attributes, price, stock);
        _variants.Add(variant);

        return variant;
    }

    public Result ChangePrice(Sku sku, Money price)
    {
        if (price.Amount <= 0)
        {
            return CatalogErrors.InvalidPrice;
        }

        return WithVariant(sku, variant => variant.ChangePrice(price));
    }

    public Result SetStock(Sku sku, int stock)
    {
        if (stock < 0)
        {
            return CatalogErrors.NegativeStock;
        }

        return WithVariant(sku, variant => variant.SetStock(stock));
    }

    /// <param name="url">An http(s) URL or a site-relative path such as "/images/products/a.svg".</param>
    /// <param name="appliesTo">Variants the image shows, e.g. {"Cor": "Preto"}; null for a general image.</param>
    public Result AddImage(Uri url, VariantAttributes? appliesTo = null)
    {
        if (!IsValidImageUrl(url))
        {
            return CatalogErrors.InvalidImageUrl;
        }

        _images.Add(new ProductImage(url, appliesTo ?? VariantAttributes.None));
        return Result.Success();
    }

    /// <summary>
    /// The picture that best represents a variant: the most specific image whose attributes the
    /// variant has (e.g. {"Cor": "Preto"} for a black one), otherwise the first general image,
    /// otherwise the first image. Null when the product has no images.
    /// </summary>
    public ProductImage? ImageFor(VariantAttributes variant) =>
        _images
            .Where(image => !image.IsGeneral
                && image.AppliesTo.Values.All(pair =>
                    variant.Values.TryGetValue(pair.Key, out var value) && value == pair.Value))
            .OrderByDescending(image => image.AppliesTo.Values.Count)
            .FirstOrDefault()
        ?? _images.Find(image => image.IsGeneral)
        ?? (_images.Count > 0 ? _images[0] : null);

    public Result Publish()
    {
        if (_variants.Count == 0)
        {
            return CatalogErrors.PublishWithoutVariants;
        }

        IsPublished = true;
        return Result.Success();
    }

    public void Unpublish() => IsPublished = false;

    private static bool IsValidImageUrl(Uri? url) =>
        url switch
        {
            null => false,
            { IsAbsoluteUri: true } => url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp,
            _ => url.OriginalString.StartsWith('/') && !url.OriginalString.StartsWith("//", StringComparison.Ordinal),
        };

    private Result WithVariant(Sku sku, Action<ProductVariant> change)
    {
        var variant = _variants.Find(v => v.Sku == sku);
        if (variant is null)
        {
            return CatalogErrors.VariantNotFound;
        }

        change(variant);
        return Result.Success();
    }
}
