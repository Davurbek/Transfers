using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Universal.Transfers.Domain.DeadLetter.Entities;

namespace Universal.Transfers.Infrastructure.DeadLetter.Persistence.Configurations;

public sealed class DeadLetterConfiguration : IEntityTypeConfiguration<DeadLetterMessage>
{
    public void Configure(EntityTypeBuilder<DeadLetterMessage> builder)
    {
        builder.ToTable("DeadLetterMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd();
        builder.Property(m => m.OriginalTopic).HasMaxLength(200).IsRequired();
        builder.Property(m => m.MessageKey).HasMaxLength(200);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.FailureReason).HasMaxLength(2000);
        builder.Property(m => m.EventType).HasMaxLength(200);
        builder.Property(m => m.LastRetryError).HasMaxLength(2000);
        builder.HasIndex(m => new { m.Replayed, m.RetryCount });
        builder.HasIndex(m => m.FailedAt);
    }
}
