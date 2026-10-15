using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class ExamSetQuestionConfiguration : IEntityTypeConfiguration<ExamSetQuestion>
{
    public void Configure(EntityTypeBuilder<ExamSetQuestion> builder)
    {
        builder.ToTable("exam_set_question");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("exam_set_question_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamSetId).HasColumnName("exam_set_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.QuestionId).HasColumnName("question_id").HasColumnType("bigint unsigned").IsRequired();
        builder.HasIndex(x => new { x.ExamSetId, x.QuestionId })
            .IsUnique().HasDatabaseName("uq_exam_set_question_exam_set_id_question_id");
        builder.HasOne(x => x.ExamSet).WithMany(x => x.Questions)
            .HasForeignKey(x => x.ExamSetId).HasConstraintName("fk_exam_set_question_exam_set_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Question).WithMany()
            .HasForeignKey(x => x.QuestionId).HasConstraintName("fk_exam_set_question_question_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
