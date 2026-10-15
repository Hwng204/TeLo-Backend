using Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Notification;

public sealed class NotificationConfigConfiguration : IEntityTypeConfiguration<NotificationConfig>
{
    public void Configure(EntityTypeBuilder<NotificationConfig> builder)
    {
        builder.ToTable("notification_config");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("notification_config_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.SubjectTemplate).HasColumnName("subject_template").HasColumnType("text").IsRequired();
        builder.Property(x => x.TitleTemplate).HasColumnName("title_template").HasColumnType("text").IsRequired();
        builder.Property(x => x.ContentTemplate).HasColumnName("content_template").HasColumnType("text").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.EventCode).IsUnique().HasDatabaseName("uq_notification_config_event_code");
    }
}
