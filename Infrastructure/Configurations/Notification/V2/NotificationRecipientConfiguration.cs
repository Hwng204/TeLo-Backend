using Domain.Entities.Identity;
using Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Notification;

public sealed class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        builder.ToTable("notification_user");
        builder.HasKey(x => new { x.NotificationId, x.UserId });
        builder.Property(x => x.NotificationId).HasColumnName("notification_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.ReadAt).HasColumnName("read_at").HasColumnType("datetime(6)");
        builder.HasOne<Domain.Entities.Notification.Notification>().WithMany()
            .HasForeignKey(x => x.NotificationId).HasConstraintName("fk_notification_user_notification_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.UserId).HasConstraintName("fk_notification_user_user_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
