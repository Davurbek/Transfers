using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Universal.Transfers.Domain.Inbox.Entities;

namespace Universal.Transfers.Infrastructure.Inbox.Persistence.Configurations;

public sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(m => m.EventType).HasMaxLength(500).IsRequired();
        builder.Property(m => m.ProcessedAt).IsRequired();
        builder.HasIndex(m => m.IdempotencyKey).IsUnique();
    }
}
