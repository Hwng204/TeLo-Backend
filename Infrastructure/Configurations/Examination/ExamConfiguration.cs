using Domain.Entities.Examination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exams", table =>
        {
            table.HasCheckConstraint("ck_exams_dates", "end_date >= start_date");
        });

        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(entity => entity.SemesterId).HasColumnName("semester_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(entity => entity.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned");
        builder.Property(entity => entity.SubjectId).HasColumnName("subject_id").HasColumnType("bigint unsigned");
        builder.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(entity => entity.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(entity => entity.StartTime).HasColumnName("start_time").HasColumnType("time").HasDefaultValue(new TimeOnly(0, 0)).IsRequired();
        builder.Property(entity => entity.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
        builder.Property(entity => entity.EndTime).HasColumnName("end_time").HasColumnType("time").HasDefaultValue(new TimeOnly(0, 0)).IsRequired();
        builder.Property(entity => entity.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.SchoolBranchId).HasColumnName("school_branch_id").HasColumnType("bigint unsigned").IsRequired();

        builder.HasIndex(entity => new { entity.SchoolBranchId, entity.SemesterId })
            .HasDatabaseName("idx_exams_branch_semester");
        builder.HasIndex(entity => entity.GradeLevelId).HasDatabaseName("idx_exams_grade_level");
        builder.HasIndex(entity => entity.SubjectId).HasDatabaseName("idx_exams_subject");

        builder.HasOne(entity => entity.Semester)
            .WithMany()
            .HasForeignKey(entity => entity.SemesterId)
            .HasConstraintName("fk_exams_semester")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.GradeLevel)
            .WithMany()
            .HasForeignKey(entity => entity.GradeLevelId)
            .HasConstraintName("fk_exams_grade_level")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.Subject)
            .WithMany()
            .HasForeignKey(entity => entity.SubjectId)
            .HasConstraintName("fk_exams_subject")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entity => entity.SchoolBranch)
            .WithMany()
            .HasForeignKey(entity => entity.SchoolBranchId)
            .HasConstraintName("fk_exams_branch")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
