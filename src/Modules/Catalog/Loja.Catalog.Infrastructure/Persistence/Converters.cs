using System.Text.Json;
using Loja.Catalog.Domain;
using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Loja.Catalog.Infrastructure.Persistence;

/// <summary>
/// Translates domain types to column values and back. Values read from the database were valid
/// when written, so re-creating them through the factories (".Value") cannot fail.
/// </summary>
internal static class Converters
{
    public static readonly ValueConverter<ProductId, Guid> ProductId = new(id => id.Value, value => new ProductId(value));

    public static readonly ValueConverter<ProductVariantId, Guid> ProductVariantId =
        new(id => id.Value, value => new ProductVariantId(value));

    public static readonly ValueConverter<CategoryId, Guid> CategoryId = new(id => id.Value, value => new CategoryId(value));

    public static readonly ValueConverter<Slug, string> Slug = new(slug => slug.Value, value => Domain.Slug.From(value).Value);

    public static readonly ValueConverter<Sku, string> Sku = new(sku => sku.Value, value => Domain.Products.Sku.Create(value).Value);

    public static readonly ValueConverter<VariantAttributes, string> VariantAttributes = new(
        attributes => JsonSerializer.Serialize(attributes.Values, (JsonSerializerOptions?)null),
        json => Domain.Products.VariantAttributes.Create(
            JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null)!).Value);

    // Tells EF how to detect changes on a type it cannot compare by itself.
    public static readonly ValueComparer<VariantAttributes> VariantAttributesComparer = new(
        (left, right) => Equals(left, right),
        attributes => attributes.GetHashCode());

    // Images as a JSON array: [{"Url": "/a-preto.svg", "AppliesTo": {"Cor": "Preto"}}]. An array keeps the order.
    public static readonly ValueConverter<List<ProductImage>, string> ProductImages = new(
        images => JsonSerializer.Serialize(
            images.Select(image => new ImageJson(image.Url.OriginalString, image.AppliesTo.Values)),
            (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<List<ImageJson>>(json, (JsonSerializerOptions?)null)!
            .Select(image => new ProductImage(
                new Uri(image.Url, UriKind.RelativeOrAbsolute),
                Domain.Products.VariantAttributes.Create(image.AppliesTo).Value))
            .ToList());

    // A list is a mutable reference: EF needs a snapshot (copy) to notice items added later.
    public static readonly ValueComparer<List<ProductImage>> ProductImagesComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        images => images.Aggregate(0, (hash, image) => HashCode.Combine(hash, image.GetHashCode())),
        images => images.ToList());

    private sealed record ImageJson(string Url, IReadOnlyDictionary<string, string> AppliesTo);
}
