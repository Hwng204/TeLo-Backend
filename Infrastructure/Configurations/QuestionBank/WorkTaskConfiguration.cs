using Domain.Entities.Academic;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.AssignedToUserId).HasColumnName("assigned_to_user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.DueAt).HasColumnName("due_at").HasColumnType("datetime(6)");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.TaskType).HasColumnName("task_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.AcademicContextId).HasColumnName("academic_context_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.SemesterId).HasColumnName("semester_id").HasColumnType("bigint unsigned");
        builder.HasIndex(x => new { x.AssignedToUserId, x.Status, x.DueAt })
            .HasDatabaseName("idx_tasks_assignee_status_due");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("idx_tasks_creator");
        builder.HasIndex(x => x.UpdatedByUserId).HasDatabaseName("idx_tasks_updater");
        builder.HasIndex(x => x.AcademicContextId).HasDatabaseName("idx_tasks_context");
        builder.HasIndex(x => x.SemesterId).HasDatabaseName("idx_tasks_semester");
        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).HasConstraintName("fk_tasks_creator")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedToUser).WithMany()
            .HasForeignKey(x => x.AssignedToUserId).HasConstraintName("fk_tasks_assignee")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UpdatedByUser).WithMany()
            .HasForeignKey(x => x.UpdatedByUserId).HasConstraintName("fk_tasks_updater")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<AcademicContext>().WithMany()
            .HasForeignKey(x => x.AcademicContextId).HasConstraintName("fk_tasks_context")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Semester>().WithMany()
            .HasForeignKey(x => x.SemesterId).HasConstraintName("fk_tasks_semester")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
