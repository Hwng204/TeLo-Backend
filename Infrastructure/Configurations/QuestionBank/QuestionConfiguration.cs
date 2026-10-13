using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");
        builder.Property(x => x.PracticeAt).HasColumnName("practice_at").HasColumnType("datetime(6)");
        builder.Property(x => x.PracticeByUserId).HasColumnName("practice_by_user_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.PracticeReason).HasColumnName("practice_reason").HasMaxLength(255);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.QuestionTaskDetailId).HasColumnName("question_task_detail_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.QuestionBankId).HasColumnName("question_bank_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("longtext").IsRequired();
        builder.Property(x => x.AnswerExplanation).HasColumnName("answer_explanation").HasColumnType("longtext");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at").HasColumnType("datetime(6)");
        builder.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasColumnType("text");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("datetime(6)");
        builder.HasIndex(x => x.QuestionTaskDetailId).HasDatabaseName("idx_questions_task_detail");
        builder.HasIndex(x => new { x.QuestionBankId, x.Status }).HasDatabaseName("idx_questions_bank_status");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("idx_questions_creator");
        builder.HasIndex(x => x.ReviewedByUserId).HasDatabaseName("idx_questions_reviewer");
        builder.HasOne(x => x.QuestionTaskDetail).WithMany(x => x.Questions)
            .HasForeignKey(x => x.QuestionTaskDetailId).HasConstraintName("fk_questions_task_detail")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuestionBank).WithMany(x => x.Questions)
            .HasForeignKey(x => x.QuestionBankId).HasConstraintName("fk_questions_bank")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).HasConstraintName("fk_questions_creator")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany()
            .HasForeignKey(x => x.ReviewedByUserId).HasConstraintName("fk_questions_reviewer")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
