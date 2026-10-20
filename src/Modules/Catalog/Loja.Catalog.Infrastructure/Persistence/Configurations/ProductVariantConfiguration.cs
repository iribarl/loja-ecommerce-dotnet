using Loja.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loja.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasConversion(Converters.ProductVariantId).ValueGeneratedNever();

        builder.Property(v => v.Sku).HasConversion(Converters.Sku).HasMaxLength(Sku.MaxLength);
        builder.HasIndex(v => v.Sku).IsUnique();

        // jsonb so the store can filter by attribute (e.g. {"Tamanho": "M"}) with the @> operator,
        // backed by a GIN index.
        builder.Property(v => v.Attributes)
            .HasConversion(Converters.VariantAttributes, Converters.VariantAttributesComparer)
            .HasColumnType("jsonb");
        builder.HasIndex(v => v.Attributes).HasMethod("GIN");

        // Money is a value object without identity: stored as two columns of this table.
        builder.ComplexProperty(v => v.Price, price =>
        {
            price.Property(m => m.Amount).HasColumnName("PriceAmount").HasPrecision(12, 2);
            price.Property(m => m.Currency).HasColumnName("PriceCurrency").HasMaxLength(3).IsFixedLength();
        });

        builder.Property(v => v.Position);

        builder.Ignore(v => v.InStock);
        builder.Ignore(v => v.DomainEvents);
    }
}
