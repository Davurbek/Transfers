using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Universal.Transfers.Domain.Inbox.Entities;

namespace Universal.Transfers.Infrastructure.Inbox.Persistence.Configurations;

public sealed class InboxEventConfiguration : IEntityTypeConfiguration<InboxEvent>
{
    public void Configure(EntityTypeBuilder<InboxEvent> builder)
    {
        builder.ToTable("InboxEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.InternalRef).HasMaxLength(64).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Payload).IsRequired();
        builder.Property(e => e.OccurredOn).IsRequired();
        builder.Property(e => e.ReceivedAt).IsRequired();
        builder.Property(e => e.ErrorMessage).HasMaxLength(1000);
        builder.HasIndex(e => new { e.Processed, e.OccurredOn });
        builder.HasIndex(e => e.InternalRef);
    }
}
