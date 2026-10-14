using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;

namespace OrderManagement.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
        builder.Property(customer => customer.Email).HasMaxLength(254).IsRequired();
        builder.Property(customer => customer.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(customer => customer.Role)
            .HasConversion(new EnumToStringConverter<UserRole>())
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(customer => customer.IsActive).HasColumnType("tinyint(1)").IsRequired();
        builder.Property(customer => customer.CreatedAt).HasColumnType("datetime(6)").HasConversion(DateTimeStorage.Converter).IsRequired();
        builder.Property(customer => customer.UpdatedAt).HasColumnType("datetime(6)").HasConversion(DateTimeStorage.Converter).IsRequired();

        builder.HasIndex(customer => customer.Email).IsUnique();

        builder.HasMany(customer => customer.Orders)
            .WithOne(order => order.Customer)
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
