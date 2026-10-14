using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_TotalAmount", "TotalAmount > 0");
        });

        builder.HasKey(order => order.Id);
        builder.Property(order => order.TotalAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(order => order.Status)
            .HasConversion(new EnumToStringConverter<OrderStatus>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(order => order.CreatedAt).HasColumnType("datetime(6)").HasConversion(DateTimeStorage.Converter).IsRequired();
        builder.Property(order => order.UpdatedAt).HasColumnType("datetime(6)").HasConversion(DateTimeStorage.Converter).IsRequired();

        builder.HasIndex(order => order.CustomerId);
        builder.HasIndex(order => order.Status);
        builder.HasIndex(order => new { order.CustomerId, order.CreatedAt });

        builder.HasMany(order => order.Items)
            .WithOne(item => item.Order)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
