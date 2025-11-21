using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(m => m.Quantity)
            .IsRequired();

        builder.Property(m => m.Reason)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.CorrelationId)
            .HasMaxLength(64);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.HasIndex(m => new { m.ProductId, m.CreatedAt });

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
