using Domain.Entities.Examination;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class ExamSubjectConfiguration : IEntityTypeConfiguration<ExamSubject>
{
    public void Configure(EntityTypeBuilder<ExamSubject> builder)
    {
        builder.ToTable("exam_subject");
        builder.HasKey(x => new { x.ExamId, x.ExamSetId });
        builder.Property(x => x.ExamId).HasColumnName("exam_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.ExamSetId).HasColumnName("exam_set_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.IsPrimaryExam).HasColumnName("is_primary_exam").HasDefaultValue(false).IsRequired();
        builder.HasIndex(x => new { x.ExamSetId }).IsUnique().HasDatabaseName("uq_exam_subject_exam_set_id");
        builder.HasOne<Exam>().WithMany()
            .HasForeignKey(x => x.ExamId).HasConstraintName("fk_exam_subject_exam_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ExamSet>().WithMany()
            .HasForeignKey(x => x.ExamSetId).HasConstraintName("fk_exam_subject_exam_set_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
