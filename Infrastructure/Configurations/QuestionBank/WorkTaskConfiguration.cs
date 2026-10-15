using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> builder)
    {
        builder.ToTable("task");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("task_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.AssignedToUserId).HasColumnName("assigned_to_user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.DueAt).HasColumnName("due_at").HasColumnType("datetime(6)");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.Property(x => x.TaskType).HasColumnName("task_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("task_name").HasMaxLength(255).IsRequired();
        builder.HasIndex(x => new { x.AssignedToUserId, x.Status, x.DueAt })
            .HasDatabaseName("idx_tasks_assignee_status_due");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("idx_tasks_creator");
        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).HasConstraintName("fk_task_created_by_user_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedToUser).WithMany()
            .HasForeignKey(x => x.AssignedToUserId).HasConstraintName("fk_task_assigned_to_user_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
