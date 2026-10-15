using Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Notification;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Domain.Entities.Notification.Notification>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Notification.Notification> builder)
    {
        builder.ToTable("notification");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("notification_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.NotificationConfigId).HasColumnName("notification_config_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.HasOne<NotificationConfig>().WithMany()
            .HasForeignKey(x => x.NotificationConfigId).HasConstraintName("fk_notification_notification_config_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
