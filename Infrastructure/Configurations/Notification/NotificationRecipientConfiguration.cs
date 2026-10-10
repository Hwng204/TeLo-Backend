using Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Notification;

public sealed class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        builder.ToTable("notification_recipients", table =>
        {
            table.HasCheckConstraint("ck_notification_recipients_email_status", "email_status IN ('PENDING', 'SENDING', 'SENT', 'ERROR', 'CANCELLED')");
            table.HasCheckConstraint("ck_notification_recipients_sent_at", "email_status <> 'SENT' OR email_sent_at IS NOT NULL");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.EmailAddress).HasColumnName("email_address").HasMaxLength(255);
        builder.Property(entity => entity.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
        builder.Property(entity => entity.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(entity => entity.LockedAt).HasColumnName("locked_at");
        builder.Property(entity => entity.DeliveryToken).HasColumnName("delivery_token").HasMaxLength(36);
        builder.Property(entity => entity.LastError).HasColumnName("last_error").HasMaxLength(100);
        builder.HasIndex(entity => new { entity.EmailStatus, entity.NextAttemptAt });
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(entity => entity.NotificationId).HasColumnName("notification_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(entity => entity.UserId).HasColumnName("user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(entity => entity.EmailStatus).HasColumnName("email_status").HasMaxLength(20).HasDefaultValue("PENDING").IsRequired();
        builder.Property(entity => entity.EmailSentAt).HasColumnName("email_sent_at").HasColumnType("datetime(6)");
        builder.Property(entity => entity.ReadAt).HasColumnName("read_at").HasColumnType("datetime(6)");

        builder.HasIndex(entity => new { entity.NotificationId, entity.UserId })
            .IsUnique().HasDatabaseName("uq_notification_recipients_notification_user");
        builder.HasIndex(entity => new { entity.EmailStatus, entity.NotificationId })
            .HasDatabaseName("idx_notification_recipients_delivery");
        builder.HasIndex(entity => new { entity.UserId, entity.ReadAt, entity.NotificationId })
            .HasDatabaseName("idx_notification_recipients_inbox");

        builder.HasOne(entity => entity.Notification)
            .WithMany(notification => notification.Recipients)
            .HasForeignKey(entity => entity.NotificationId)
            .HasConstraintName("fk_notification_recipients_notification")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .HasConstraintName("fk_notification_recipients_user")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
