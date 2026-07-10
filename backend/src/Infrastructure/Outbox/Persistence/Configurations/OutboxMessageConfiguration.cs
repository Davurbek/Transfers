using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Universal.Transfers.Domain.Outbox.Entities;

namespace Universal.Transfers.Infrastructure.Outbox.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.EventType).HasMaxLength(500).IsRequired();
        builder.Property(m => m.AggregateId).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.Headers).HasColumnType("jsonb");
        builder.Property(m => m.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(m => new { m.PublishedAt, m.DeadLetteredAt, m.AvailableAt });
        builder.HasIndex(m => m.CreatedAt);
    }
}
