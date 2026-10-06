using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class AcademicContextConfiguration : IEntityTypeConfiguration<AcademicContext>
{
    public void Configure(EntityTypeBuilder<AcademicContext> builder)
    {
        builder.ToTable("academic_contexts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.AcademicYearId).HasColumnName("academic_year_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SchoolId).HasColumnName("school_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SchoolBranchId).HasColumnName("school_branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.HasIndex(x => new
        {
            x.AcademicYearId, x.SchoolId, x.SchoolBranchId,
            x.SubjectId, x.GradeLevelId
        }).IsUnique().HasDatabaseName("uq_academic_contexts_scope");
        builder.HasIndex(x => new { x.SubjectId, x.GradeLevelId })
            .HasDatabaseName("idx_academic_contexts_subject_grade");

        builder.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).HasConstraintName("fk_academic_contexts_year")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.School).WithMany()
            .HasForeignKey(x => x.SchoolId).HasConstraintName("fk_academic_contexts_school")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolBranch).WithMany()
            .HasForeignKey(x => new { x.SchoolBranchId, x.SchoolId })
            .HasPrincipalKey(x => new { x.Id, x.SchoolId })
            .HasConstraintName("fk_academic_contexts_branch_school")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).HasConstraintName("fk_academic_contexts_subject")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GradeLevel).WithMany()
            .HasForeignKey(x => x.GradeLevelId).HasConstraintName("fk_academic_contexts_grade")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
