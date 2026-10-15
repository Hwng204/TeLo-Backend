using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class TeacherAssignmentConfiguration : IEntityTypeConfiguration<TeacherAssignment>
{
    public void Configure(EntityTypeBuilder<TeacherAssignment> builder)
    {
        builder.ToTable("teacher_assignment");
        builder.HasKey(x => new { x.ClassId, x.TeacherId, x.SubjectId });
        builder.Property(x => x.ClassId).HasColumnName("class_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.TeacherId).HasColumnName("teacher_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.AssignedAt).HasColumnName("assignedAt").HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.HasOne(x => x.SchoolClass).WithMany(x => x.TeacherAssignments)
            .HasForeignKey(x => x.ClassId).HasConstraintName("fk_teacher_assignment_class_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Teacher).WithMany(x => x.Assignments)
            .HasForeignKey(x => x.TeacherId).HasConstraintName("fk_teacher_assignment_teacher_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).HasConstraintName("fk_teacher_assignment_subject_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
