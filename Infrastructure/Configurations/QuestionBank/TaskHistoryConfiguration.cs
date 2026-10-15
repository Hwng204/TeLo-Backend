using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class TaskHistoryConfiguration : IEntityTypeConfiguration<TaskHistory>
{
    public void Configure(EntityTypeBuilder<TaskHistory> builder)
    {
        builder.ToTable("task_history");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("task_history_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.CreateBy).HasColumnName("create_by").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.CreateAt).HasColumnName("create_at").HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.HasOne(x => x.Task).WithMany()
            .HasForeignKey(x => x.TaskId).HasConstraintName("fk_task_history_task_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CreateBy).HasConstraintName("fk_task_history_create_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
