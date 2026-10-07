using Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Notification;

public sealed class NotificationConfigConfiguration : IEntityTypeConfiguration<NotificationConfig>
{
    public void Configure(EntityTypeBuilder<NotificationConfig> builder)
    {
        builder.ToTable("notification_configs", table =>
        {
            table.HasCheckConstraint("ck_notification_configs_timing", "timing IN ('IMMEDIATE', 'BEFORE_DEADLINE')");
            table.HasCheckConstraint("ck_notification_configs_recipient_scope", "recipient_scope IN ('NONE', 'ALL_SCHOOL', 'SOURCE_ACTOR', 'SOURCE_ASSIGNEE', 'SOURCE_PARTICIPANTS')");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Version).HasColumnName("version").HasDefaultValue(1u).IsConcurrencyToken();
        builder.Property(entity => entity.EmailTemplateVersionId).HasColumnName("email_template_version_id");
        builder.HasOne(entity => entity.EmailTemplateVersion).WithMany().HasForeignKey(entity => entity.EmailTemplateVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(entity => entity.SchoolId).HasColumnName("school_id").HasColumnType("bigint unsigned");
        builder.Property(entity => entity.SchoolBranchId).HasColumnName("school_branch_id").HasColumnType("bigint unsigned");
        builder.Property(entity => entity.BaseConfigId).HasColumnName("base_config_id").HasColumnType("bigint unsigned");
        builder.Property(entity => entity.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.EventCode).HasColumnName("event_code").HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Timing).HasColumnName("timing").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.RecipientScope).HasColumnName("recipient_scope").HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.TitleTemplate).HasColumnName("title_template").HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.ContentTemplate).HasColumnName("content_template").HasColumnType("longtext").IsRequired();
        builder.Property(entity => entity.ActionUrlTemplate).HasColumnName("action_url_template").HasMaxLength(2048);
        builder.Property(entity => entity.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.Property<ulong?>("BaseScopeSchoolKey")
            .HasColumnName("base_scope_school_key")
            .HasColumnType("bigint unsigned")
            .ValueGeneratedOnAddOrUpdate()
            .HasComputedColumnSql("CASE WHEN school_branch_id IS NULL AND base_config_id IS NULL THEN COALESCE(school_id, 0) ELSE NULL END", stored: true);
        builder.Property<string?>("BaseScopeCodeKey")
            .HasColumnName("base_scope_code_key")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .ValueGeneratedOnAddOrUpdate()
            .HasComputedColumnSql("CASE WHEN school_branch_id IS NULL AND base_config_id IS NULL THEN code ELSE NULL END", stored: true);

        builder.HasIndex(entity => new { entity.BaseConfigId, entity.SchoolBranchId })
            .IsUnique().HasDatabaseName("uq_notification_configs_branch_override");
        builder.HasIndex("BaseScopeSchoolKey", "BaseScopeCodeKey")
            .IsUnique().HasDatabaseName("uq_notification_configs_base_scope");
        builder.HasIndex(entity => new { entity.Id, entity.SchoolId, entity.Code })
            .IsUnique().HasDatabaseName("uq_notification_configs_parent_match");
        builder.HasIndex(entity => new { entity.SchoolId, entity.IsActive })
            .HasDatabaseName("idx_notification_configs_school_active");
        builder.HasIndex(entity => new { entity.SchoolBranchId, entity.IsActive })
            .HasDatabaseName("idx_notification_configs_branch_active");

        builder.HasOne(entity => entity.School)
            .WithMany()
            .HasForeignKey(entity => entity.SchoolId)
            .HasConstraintName("fk_notification_configs_school")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.SchoolBranch)
            .WithMany()
            .HasForeignKey(entity => new { entity.SchoolBranchId, entity.SchoolId })
            .HasPrincipalKey(branch => new { branch.Id, branch.SchoolId })
            .HasConstraintName("fk_notification_configs_branch_school")
            .OnDelete(DeleteBehavior.Restrict);

        // EF Core alternate keys cannot contain nullable properties. The domain column
        // remains nullable and the exact (base_config_id, school_id, code) FK is added
        // explicitly by the InitialCreate migration; this navigation uses the id part
        // for change tracking while the database constraint enforces the full scope.
        builder.HasOne(entity => entity.BaseConfig)
            .WithMany(entity => entity.DerivedConfigs)
            .HasForeignKey(entity => entity.BaseConfigId)
            .HasPrincipalKey(entity => entity.Id)
            .HasConstraintName("fk_notification_configs_base_match")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
