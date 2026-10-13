using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class ExamTaskPaperConfiguration : IEntityTypeConfiguration<ExamTaskPaper>
{
    public void Configure(EntityTypeBuilder<ExamTaskPaper> builder)
    {
        builder.ToTable("exam_task_papers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasColumnType("int unsigned").IsRequired();
        // Tên đề do TaskService đặt (Đề 1 … Đề N) nên không cần unique.
        builder.HasIndex(x => x.TaskId).HasDatabaseName("idx_exam_task_papers_task");
        builder.HasOne(x => x.Task).WithMany()
            .HasForeignKey(x => x.TaskId).HasConstraintName("fk_exam_task_papers_task")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
