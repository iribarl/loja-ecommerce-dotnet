using Loja.Catalog.Domain.Products;
using Loja.Catalog.Domain.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loja.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("stock_reservations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.VariantId).HasConversion(Converters.ProductVariantId);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(r => r.VariantId).OnDelete(DeleteBehavior.Restrict);

        // One reservation per order and variant; also makes a retried reservation impossible to duplicate.
        builder.HasIndex(r => new { r.OrderId, r.VariantId }).IsUnique();

        // The sweeper looks for open reservations past their expiry.
        builder.HasIndex(r => r.ExpiresAt).HasFilter("\"Status\" = 'Reserved'");
    }
}
