using Korp.Billing.Infrastructure.Persistence.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korp.Billing.Infrastructure.Persistence.Configurations;

public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("inbox_messages", table => table.HasCheckConstraint("ck_inbox_messages_schema_version", "schema_version > 0"));
        builder.HasKey(message => message.MessageId).HasName("pk_inbox_messages");
        builder.Property(message => message.MessageType).HasMaxLength(200).IsRequired();
        builder.Property(message => message.PayloadHash).HasColumnType("char(64)").IsRequired();
        builder.HasIndex(message => message.ProcessedAtUtc).HasDatabaseName("ix_inbox_messages_processed_at_utc");
    }
}
