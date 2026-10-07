using Domain.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Notification;

public sealed class EmailEventConfiguration : IEntityTypeConfiguration<EmailEvent>
{
    public void Configure(EntityTypeBuilder<EmailEvent> b)
    {
        b.ToTable("email_events", t =>
        {
            t.HasCheckConstraint("ck_email_events_status", "status IN ('ACTIVE','INACTIVE')");
            t.HasCheckConstraint("ck_email_events_scope", "(trigger_kind='SYSTEM' AND school_id IS NULL) OR (trigger_kind='MANUAL' AND school_id IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.SchoolId).HasColumnName("school_id");
        b.Property(x => x.ScopeKey).HasColumnName("scope_key").HasComputedColumnSql("COALESCE(school_id,0)", stored: true);
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000).IsRequired();
        b.Property(x => x.TriggerKind).HasColumnName("trigger_kind").HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        b.Property(x => x.VariablesJson).HasColumnName("variables_json").HasColumnType("longtext").IsRequired();
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.UsedAt).HasColumnName("used_at");
        b.HasIndex(x => new { x.ScopeKey, x.Code }).IsUnique();
        b.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(
            SystemEvent(1, "MATRIX_ASSIGNED", "Giao nhiệm vụ lập ma trận", ["schoolName", "branchName", "actorName", "taskName", "dueAt", "actionUrl"]),
            SystemEvent(2, "MATRIX_SUBMITTED", "Ma trận đã nộp", ["schoolName", "branchName", "actorName", "matrixName", "actionUrl"]),
            SystemEvent(3, "MATRIX_APPROVED", "Ma trận đã duyệt / hoàn tất", ["schoolName", "branchName", "actorName", "matrixName", "actionUrl"]),
            SystemEvent(4, "MATRIX_REJECTED", "Ma trận cần chỉnh sửa", ["schoolName", "branchName", "actorName", "matrixName", "actionUrl"]));
    }

    private static EmailEvent SystemEvent(ulong id, string code, string name, string[] variables) => new()
    {
        Id = id, Code = code, Name = name, TriggerKind = "SYSTEM",
        VariablesJson = System.Text.Json.JsonSerializer.Serialize(variables.Select(name => new { Name = name, Label = name, Type = "TEXT", Required = true }))
    };
}
