using Loja.Cart.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Loja.Cart.Infrastructure.Persistence;

internal sealed class ShoppingCartConfiguration : IEntityTypeConfiguration<ShoppingCart>
{
    public void Configure(EntityTypeBuilder<ShoppingCart> builder)
    {
        builder.ToTable("carts");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new CartId(value))
            .ValueGeneratedNever();

        // A customer has at most one cart. Anonymous carts (null) are not affected by the index.
        builder.HasIndex(c => c.CustomerId).IsUnique().HasFilter("\"CustomerId\" IS NOT NULL");
        builder.HasIndex(c => c.UpdatedAt);

        // Optimistic concurrency with Postgres' xmin system column: if two tabs change the same cart,
        // the second save fails instead of silently overwriting the first.
        builder.Property<uint>("Version").IsRowVersion();

        // Lines live in their own table but belong to the cart: no id of their own, key (cart, variant).
        builder.OwnsMany(c => c.Items, items =>
        {
            items.ToTable("cart_items");
            items.WithOwner().HasForeignKey("CartId");
            items.HasKey("CartId", nameof(CartItem.VariantId));

            // The variant id comes from the domain. Without this, EF assumes a Guid key is generated
            // on insert, so a new line that already has one looks like an existing row: it gets an
            // UPDATE that affects nothing instead of an INSERT.
            items.Property(i => i.VariantId).ValueGeneratedNever();
        });
        builder.Navigation(c => c.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.TotalUnits);
        builder.Ignore(c => c.DomainEvents);
    }
}
