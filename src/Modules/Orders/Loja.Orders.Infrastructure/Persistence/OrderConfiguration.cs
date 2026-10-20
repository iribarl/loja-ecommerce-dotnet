using System.Text.Json;
using Loja.Orders.Domain;
using Loja.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Loja.Orders.Infrastructure.Persistence;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasConversion(id => id.Value, value => new OrderId(value)).ValueGeneratedNever();

        // As text: readable in the database and safe if the enum is ever reordered.
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(o => o.TrackingCode).HasMaxLength(100);
        builder.Property(o => o.CancellationReason).HasMaxLength(500);

        // Owned (not complex) because "my orders" needs an index on the customer id.
        builder.OwnsOne(o => o.Customer, customer =>
        {
            customer.Property(c => c.CustomerId).HasColumnName("CustomerId");
            customer.Property(c => c.Name).HasColumnName("CustomerName").HasMaxLength(150);
            customer.Property(c => c.Email).HasColumnName("CustomerEmail").HasMaxLength(256);
            customer.HasIndex(c => c.CustomerId);
        });
        builder.Navigation(o => o.Customer).IsRequired();

        // Value objects without identity, stored as columns of the orders table.
        builder.ComplexProperty(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.Recipient).HasColumnName("ShipToRecipient").HasMaxLength(150);
            address.Property(a => a.Cep).HasColumnName("ShipToCep").HasMaxLength(8)
                .HasConversion(cep => cep.Digits, digits => Cep.Create(digits).Value);
            address.Property(a => a.Street).HasColumnName("ShipToStreet").HasMaxLength(200);
            address.Property(a => a.Number).HasColumnName("ShipToNumber").HasMaxLength(20);
            address.Property(a => a.Complement).HasColumnName("ShipToComplement").HasMaxLength(100);
            address.Property(a => a.District).HasColumnName("ShipToDistrict").HasMaxLength(100);
            address.Property(a => a.City).HasColumnName("ShipToCity").HasMaxLength(100);
            address.Property(a => a.State).HasColumnName("ShipToState").HasMaxLength(2).IsFixedLength();
        });

        builder.ComplexProperty(o => o.Shipping, shipping =>
        {
            shipping.Property(s => s.Zone).HasColumnName("ShippingZone").HasMaxLength(100);
            shipping.Property(s => s.MinDays).HasColumnName("ShippingMinDays");
            shipping.Property(s => s.MaxDays).HasColumnName("ShippingMaxDays");
            shipping.ComplexProperty(s => s.Price, price =>
            {
                price.Property(m => m.Amount).HasColumnName("ShippingPrice").HasPrecision(12, 2);
                price.Property(m => m.Currency).HasColumnName("ShippingCurrency").HasMaxLength(3).IsFixedLength();
            });
        });

        // A snapshot that is always read whole with the order: jsonb instead of extra tables.
        builder.Ignore(o => o.Lines);
        builder.Property<List<OrderLine>>("_lines")
            .HasColumnName("Lines")
            .HasColumnType("jsonb")
            .HasConversion(LinesConverter, ListComparer<OrderLine>());

        builder.Ignore(o => o.History);
        builder.Property<List<OrderStatusChange>>("_history")
            .HasColumnName("History")
            .HasColumnType("jsonb")
            .HasConversion(HistoryConverter, ListComparer<OrderStatusChange>());

        // Optimistic concurrency (Postgres xmin): the customer cancelling while the payment
        // webhook marks the order paid must not silently overwrite one another.
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(o => o.PlacedAt);

        builder.Ignore(o => o.Subtotal);
        builder.Ignore(o => o.Total);
        builder.Ignore(o => o.Currency);
        builder.Ignore(o => o.CanBeCancelledByCustomer);
        builder.Ignore(o => o.DomainEvents);
    }

    private static readonly ValueConverter<List<OrderLine>, string> LinesConverter = new(
        lines => JsonSerializer.Serialize(lines.Select(LineJson.From), (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<List<LineJson>>(json, (JsonSerializerOptions?)null)!.Select(l => l.ToLine()).ToList());

    private static readonly ValueConverter<List<OrderStatusChange>, string> HistoryConverter = new(
        history => JsonSerializer.Serialize(history, (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<List<OrderStatusChange>>(json, (JsonSerializerOptions?)null)!);

    // Lists are mutable references: EF needs a copy to notice items added later.
    private static ValueComparer<List<T>> ListComparer<T>() => new(
        (left, right) => left!.SequenceEqual(right!),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item!.GetHashCode())),
        list => list.ToList());

    // Money is stored as two plain fields so the JSON does not depend on the domain type's shape.
    private sealed record LineJson(
        Guid VariantId, Guid ProductId, string Sku, string ProductName,
        Dictionary<string, string> Attributes, decimal UnitPrice, string Currency, int Quantity)
    {
        public static LineJson From(OrderLine line) =>
            new(line.VariantId, line.ProductId, line.Sku, line.ProductName,
                new Dictionary<string, string>(line.Attributes), line.UnitPrice.Amount, line.UnitPrice.Currency, line.Quantity);

        public OrderLine ToLine() =>
            new(VariantId, ProductId, Sku, ProductName, Attributes, Money.Of(UnitPrice, Currency), Quantity);
    }
}
