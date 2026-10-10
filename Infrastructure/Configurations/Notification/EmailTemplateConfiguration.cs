using Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Notification;

public sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> b)
    {
        b.ToTable("email_templates", t => t.HasCheckConstraint("ck_email_templates_status", "status IN ('ACTIVE','INACTIVE')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.SchoolId).HasColumnName("school_id");
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.EventCode).HasColumnName("event_code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.UsedAt).HasColumnName("used_at");
        b.HasIndex(x => new { x.SchoolId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.SchoolId, x.EventCode, x.Status });
        b.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EmailTemplateVersionConfiguration : IEntityTypeConfiguration<EmailTemplateVersion>
{
    public void Configure(EntityTypeBuilder<EmailTemplateVersion> b)
    {
        b.ToTable("email_template_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.EmailTemplateId).HasColumnName("email_template_id");
        b.Property(x => x.Revision).HasColumnName("revision");
        b.Property(x => x.Subject).HasColumnName("subject").HasMaxLength(200).IsRequired();
        b.Property(x => x.Body).HasColumnName("body").HasColumnType("text").IsRequired();
        b.Property(x => x.VariablesJson).HasColumnName("variables_json").HasColumnType("longtext");
        b.Property(x => x.EventVersion).HasColumnName("event_version").HasDefaultValue(1u);
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.EmailTemplateId, x.Revision }).IsUnique();
        b.HasOne(x => x.Template).WithMany(x => x.Revisions).HasForeignKey(x => x.EmailTemplateId).OnDelete(DeleteBehavior.Cascade);
    }
}
