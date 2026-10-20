using Loja.Catalog.Domain.Categories;
using Loja.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace Loja.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    /// <summary>Shadow property: lives in the table but not in the domain class.</summary>
    public const string SearchVector = "SearchVector";

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(Converters.ProductId).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Slug).HasConversion(Converters.Slug).HasMaxLength(220);
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Description).HasMaxLength(4000);

        builder.Property(p => p.CategoryId).HasConversion(Converters.CategoryId);
        builder.HasOne<Category>().WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        // Stored as a Postgres text[] column; the domain keeps them in a private list.
        builder.PrimitiveCollection(p => p.Images)
            .HasField("_images")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .ElementType(element => element.HasConversion<string>());

        builder.HasMany(p => p.Variants)
            .WithOne()
            .HasForeignKey("ProductId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Variants).HasField("_variants").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Full-text search in Portuguese: Postgres keeps this column up to date from name and description
        // ("camisetas" also finds "camiseta"). The GIN index makes the search fast.
        builder.Property<NpgsqlTsVector>(SearchVector)
            .HasComputedColumnSql(
                """to_tsvector('portuguese', coalesce("Name", '') || ' ' || coalesce("Description", ''))""",
                stored: true);
        builder.HasIndex(SearchVector).HasMethod("GIN");

        builder.Ignore(p => p.DomainEvents);
    }
}
