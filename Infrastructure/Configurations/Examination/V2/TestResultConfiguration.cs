using Domain.Entities.Examination;
using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class TestResultConfiguration : IEntityTypeConfiguration<TestResult>
{
    public void Configure(EntityTypeBuilder<TestResult> builder)
    {
        builder.ToTable("test_result");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("test_result_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamId).HasColumnName("exam_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.ExamVariantId).HasColumnName("exam_variant_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("datetime(6)");
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at").HasColumnType("datetime(6)");
        builder.Property(x => x.TotalScore).HasColumnName("total_score").HasColumnType("decimal(5,2)");
        builder.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(x => x.CorrectCount).HasColumnName("correct_count").HasColumnType("int unsigned").HasDefaultValue(0u).IsRequired();
        builder.Property(x => x.WrongCount).HasColumnName("wrong_count").HasColumnType("int unsigned").HasDefaultValue(0u).IsRequired();
        builder.HasOne(x => x.Exam).WithMany()
            .HasForeignKey(x => x.ExamId).HasConstraintName("fk_test_result_exam_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExamVariant>().WithMany()
            .HasForeignKey(x => x.ExamVariantId).HasConstraintName("fk_test_result_exam_variant_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Student>().WithMany()
            .HasForeignKey(x => x.StudentId).HasConstraintName("fk_test_result_student_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
