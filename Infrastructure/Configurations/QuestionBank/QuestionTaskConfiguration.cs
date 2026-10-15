using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class QuestionTaskConfiguration : IEntityTypeConfiguration<QuestionTask>
{
    public void Configure(EntityTypeBuilder<QuestionTask> builder)
    {
        builder.ToTable("question_task", table => table.HasCheckConstraint("ck_question_task_count", "total_question > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("question_task_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.LessonId).HasColumnName("lesson_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.AssignedQuestionCount).HasColumnName("total_question").HasColumnType("int unsigned").IsRequired();
        // Một nhiệm vụ câu hỏi gồm nhiều bài học, mỗi bài một dòng.
        builder.HasIndex(x => new { x.TaskId, x.LessonId }).IsUnique().HasDatabaseName("uq_question_task_task_id_lesson_id");
        builder.HasIndex(x => x.LessonId).HasDatabaseName("idx_question_tasks_lesson");
        builder.HasOne(x => x.Task).WithMany()
            .HasForeignKey(x => x.TaskId).HasConstraintName("fk_question_task_task_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Lesson).WithMany()
            .HasForeignKey(x => x.LessonId).HasConstraintName("fk_question_task_lesson_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
