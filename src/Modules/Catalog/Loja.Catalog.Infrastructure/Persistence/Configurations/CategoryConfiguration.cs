using Loja.Catalog.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loja.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion(Converters.CategoryId).ValueGeneratedNever();

        builder.Property(c => c.Name).HasMaxLength(100);
        builder.Property(c => c.Slug).HasConversion(Converters.Slug).HasMaxLength(120);
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Ignore(c => c.DomainEvents);
    }
}
