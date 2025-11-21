using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(p => p.Sku).IsUnique();

        builder.Property(p => p.Name)
            .HasMaxLength(180)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasMaxLength(1024)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.QuantityAvailable)
            .IsRequired();

        builder.Property(p => p.QuantityReserved)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();
    }
}
