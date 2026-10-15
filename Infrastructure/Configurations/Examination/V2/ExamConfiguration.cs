using Domain.Entities.Academic;
using Domain.Entities.Examination;
using Domain.Entities.Organization;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exam");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("exam_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SemesterId).HasColumnName("semester_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.MatrixId).HasColumnName("matrix_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.ResultPublishedAt).HasColumnName("result_published_at").HasColumnType("datetime(6)");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.HasOne<SchoolBranch>().WithMany()
            .HasForeignKey(x => x.BranchId).HasConstraintName("fk_exam_branch_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Semester).WithMany()
            .HasForeignKey(x => x.SemesterId).HasConstraintName("fk_exam_semester_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExamMatrix>().WithMany()
            .HasForeignKey(x => x.MatrixId).HasConstraintName("fk_exam_matrix_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).HasConstraintName("fk_exam_subject_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GradeLevel>().WithMany()
            .HasForeignKey(x => x.GradeLevelId).HasConstraintName("fk_exam_grade_level_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
