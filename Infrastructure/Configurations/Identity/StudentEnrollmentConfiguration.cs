using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Infrastructure.Configurations.Identity;

public sealed class StudentEnrollmentConfiguration : IEntityTypeConfiguration<StudentEnrollment>
{
    // enroll_at/left_at are datetime(6); the C# side keeps whole days.
    private static readonly ValueConverter<DateOnly, DateTime> DateOnlyAsDateTime = new(
        v => v.ToDateTime(TimeOnly.MinValue), v => DateOnly.FromDateTime(v));

    public void Configure(EntityTypeBuilder<StudentEnrollment> builder)
    {
        builder.ToTable("student_enrollment");
        builder.HasKey(x => new { x.SchoolClassId, x.StudentId });
        builder.Property(x => x.SchoolClassId).HasColumnName("class_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.StartedOn).HasColumnName("enroll_at").HasColumnType("datetime(6)")
            .HasConversion(DateOnlyAsDateTime).HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.Property(x => x.EndedOn).HasColumnName("left_at").HasColumnType("datetime(6)")
            .HasConversion(DateOnlyAsDateTime);

        builder.HasOne(x => x.Student).WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.StudentId).HasConstraintName("fk_student_enrollment_student_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolClass).WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.SchoolClassId).HasConstraintName("fk_student_enrollment_class_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
