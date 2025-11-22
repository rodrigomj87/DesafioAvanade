using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");

        builder.HasKey(pm => pm.MessageId);

        builder.Property(pm => pm.MessageId)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(pm => pm.ProcessedAt)
            .IsRequired();

        builder.HasIndex(pm => pm.ProcessedAt);
    }
}
