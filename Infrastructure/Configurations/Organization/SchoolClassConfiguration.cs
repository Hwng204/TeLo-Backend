using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> builder)
    {
        builder.ToTable("classes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolBranchId).HasColumnName("school_branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();
        builder.Property(x => x.AcademicYearId).HasColumnName("academic_year_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned").IsRequired();

        builder.HasIndex(x => new { x.SchoolBranchId, x.AcademicYearId, x.Name })
            .IsUnique().HasDatabaseName("uq_classes_branch_year_name");
        builder.HasAlternateKey(x => new { x.Id, x.AcademicYearId }).HasName("uq_classes_id_year");
        builder.HasIndex(x => new { x.SchoolBranchId, x.AcademicYearId, x.Code })
            .IsUnique().HasDatabaseName("uq_classes_branch_year_code");
        builder.HasIndex(x => new { x.SchoolBranchId, x.AcademicYearId, x.GradeLevelId, x.Status })
            .HasDatabaseName("idx_classes_directory");
        builder.HasIndex(x => x.GradeLevelId).HasDatabaseName("idx_classes_grade");
        builder.HasOne(x => x.SchoolBranch).WithMany(x => x.Classes)
            .HasForeignKey(x => x.SchoolBranchId).HasConstraintName("fk_classes_branch")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).HasConstraintName("fk_classes_academic_year")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GradeLevel).WithMany()
            .HasForeignKey(x => x.GradeLevelId).HasConstraintName("fk_classes_grade_level")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
