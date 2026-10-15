using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class SchoolClassConfiguration : IEntityTypeConfiguration<SchoolClass>
{
    public void Configure(EntityTypeBuilder<SchoolClass> builder)
    {
        builder.ToTable("class");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("class_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolBranchId).HasColumnName("branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();
        builder.Property(x => x.AcademicYearId).HasColumnName("academic_year_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned").IsRequired();

        builder.HasIndex(x => new { x.SchoolBranchId, x.AcademicYearId, x.Name })
            .IsUnique().HasDatabaseName("uq_class_branch_id_academic_year_id_name");
        builder.HasIndex(x => new { x.SchoolBranchId, x.AcademicYearId, x.GradeLevelId, x.Status })
            .HasDatabaseName("idx_classes_directory");
        builder.HasIndex(x => x.GradeLevelId).HasDatabaseName("idx_classes_grade");
        builder.HasOne(x => x.SchoolBranch).WithMany(x => x.Classes)
            .HasForeignKey(x => x.SchoolBranchId).HasConstraintName("fk_class_branch_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).HasConstraintName("fk_class_academic_year_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GradeLevel).WithMany()
            .HasForeignKey(x => x.GradeLevelId).HasConstraintName("fk_class_grade_level_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
