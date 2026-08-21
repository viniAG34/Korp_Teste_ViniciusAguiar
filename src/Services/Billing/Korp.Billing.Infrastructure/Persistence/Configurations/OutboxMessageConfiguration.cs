using Korp.Billing.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korp.Billing.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", table =>
        {
            table.HasCheckConstraint("ck_outbox_messages_schema_version", "schema_version > 0");
            table.HasCheckConstraint("ck_outbox_messages_attempt_count", "attempt_count >= 0");
            table.HasCheckConstraint("ck_outbox_messages_lease_consistency", "(lock_id IS NULL) = (locked_until_utc IS NULL)");
            table.HasCheckConstraint("ck_outbox_messages_published_lease", "published_at_utc IS NULL OR (lock_id IS NULL AND locked_until_utc IS NULL)");
        });
        builder.HasKey(message => message.Id).HasName("pk_outbox_messages");
        builder.Property(message => message.MessageType).HasMaxLength(200).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(1000);
        builder.Property(message => message.Version).IsRowVersion().HasColumnName("xmin");
        builder.HasIndex(message => new { message.NextAttemptAtUtc, message.OccurredAtUtc }).HasFilter("published_at_utc IS NULL").HasDatabaseName("ix_outbox_messages_pending");
        builder.HasIndex(message => message.PublishedAtUtc).HasDatabaseName("ix_outbox_messages_published_at_utc");
    }
}
