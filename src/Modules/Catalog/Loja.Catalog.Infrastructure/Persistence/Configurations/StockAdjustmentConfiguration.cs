using Loja.Catalog.Domain.Products;
using Loja.Catalog.Domain.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loja.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("stock_adjustments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.VariantId).HasConversion(Converters.ProductVariantId);
        builder.Property(a => a.Reason).HasMaxLength(StockAdjustment.MaxReasonLength);

        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(a => a.VariantId).OnDelete(DeleteBehavior.Restrict);

        // The variant page shows its latest adjustments.
        builder.HasIndex(a => new { a.VariantId, a.AdjustedAt });
    }
}
