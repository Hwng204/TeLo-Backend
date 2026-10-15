using Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Notification;

public sealed class NotificationParameterConfiguration : IEntityTypeConfiguration<NotificationParameter>
{
    public void Configure(EntityTypeBuilder<NotificationParameter> builder)
    {
        builder.ToTable("parameter");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("parameter_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.NotificationConfigId).HasColumnName("notification_config_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.HasIndex(x => new { x.NotificationConfigId, x.Code })
            .IsUnique().HasDatabaseName("uq_parameter_notification_config_id_code");
        builder.HasOne<NotificationConfig>().WithMany()
            .HasForeignKey(x => x.NotificationConfigId).HasConstraintName("fk_parameter_notification_config_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
