using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("question");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("question_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.AnswerExplanation).HasColumnName("answer_explanation").HasColumnType("text");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.LessonId).HasColumnName("lesson_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.BankType).HasColumnName("bank_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.QuestionTaskDetailId).HasColumnName("question_task_details_id").HasColumnType("bigint unsigned").IsRequired();
        builder.HasIndex(x => x.QuestionTaskDetailId).HasDatabaseName("idx_questions_task_detail");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("idx_questions_creator");
        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).HasConstraintName("fk_question_created_by_user_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Lesson).WithMany()
            .HasForeignKey(x => x.LessonId).HasConstraintName("fk_question_lesson_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuestionTaskDetail).WithMany(x => x.Questions)
            .HasForeignKey(x => x.QuestionTaskDetailId).HasConstraintName("fk_question_question_task_details_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
