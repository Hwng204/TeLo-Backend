using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
    public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
    {
        builder.ToTable("student_class_enrollments", table => table.HasCheckConstraint(
            "ck_student_enrollments_status",
            "status IN ('ACTIVE', 'COMPLETED', 'TRANSFERRED_OUT')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SchoolClassId).HasColumnName("school_class_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.AcademicYearId).HasColumnName("academic_year_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32)
            .HasDefaultValue(StudentEnrollmentStatusCodes.Active).IsRequired();

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId })
            .IsUnique().HasDatabaseName("uq_student_enrollments_student_year");
        builder.HasIndex(x => new { x.SchoolClassId, x.Status })
            .HasDatabaseName("idx_student_enrollments_class_status");

        builder.HasOne(x => x.Student).WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.StudentId).HasConstraintName("fk_student_enrollments_student")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.SchoolClass).WithMany(x => x.Enrollments)
            .HasForeignKey(x => new { x.SchoolClassId, x.AcademicYearId })
            .HasPrincipalKey(x => new { x.Id, x.AcademicYearId })
            .HasConstraintName("fk_student_enrollments_class_year")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).HasConstraintName("fk_student_enrollments_year")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
