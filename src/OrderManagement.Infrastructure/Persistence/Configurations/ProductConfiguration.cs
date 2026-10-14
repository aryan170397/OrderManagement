using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderManagement.Domain.Entities;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint("CK_Products_Price", "Price > 0");
            table.HasCheckConstraint("CK_Products_AvailableQuantity", "AvailableQuantity >= 0");
        });

        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
        builder.Property(product => product.Description).HasMaxLength(2000);
        builder.Property(product => product.Price).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(product => product.AvailableQuantity).IsRequired();
        builder.Property(product => product.IsActive).HasColumnType("tinyint(1)").IsRequired();
        builder.Property(product => product.Version).IsRequired().IsConcurrencyToken();
        builder.Property(product => product.CreatedAt).HasColumnType("datetime(6)").HasConversion(DateTimeStorage.Converter).IsRequired();
        builder.Property(product => product.UpdatedAt).HasColumnType("datetime(6)").HasConversion(DateTimeStorage.Converter).IsRequired();

        builder.HasIndex(product => product.Name);
        builder.HasIndex(product => product.IsActive);

        builder.HasMany(product => product.OrderItems)
            .WithOne(item => item.Product)
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
