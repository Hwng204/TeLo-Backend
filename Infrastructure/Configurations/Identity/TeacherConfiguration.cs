using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("teachers");
        builder.Property(x => x.StaffCode).HasColumnName("staff_code").HasMaxLength(64);
        builder.HasIndex(x => x.StaffCode).IsUnique().HasDatabaseName("uq_teachers_staff_code");
        builder.Property(x => x.Department).HasColumnName("department").HasMaxLength(150);
        builder.Property(x => x.MainSubjectId).HasColumnName("main_subject_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.JoinedOn).HasColumnName("joined_on").HasColumnType("date");
        builder.Property(x => x.EmploymentStatus).HasColumnName("employment_status").HasMaxLength(32).HasDefaultValue("WORKING");
        builder.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1u).IsConcurrencyToken();
        builder.HasIndex(x => new { x.EmploymentStatus, x.Department });
        builder.HasOne(x => x.MainSubject).WithMany().HasForeignKey(x => x.MainSubjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Specialization).HasColumnName("specialization").HasMaxLength(255);
        builder.Property(x => x.Position).HasColumnName("position").HasMaxLength(150);
        builder.Property(x => x.Gender).HasColumnName("gender").HasColumnType("tinyint(1)");
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
        builder.Property(x => x.ClassId).HasColumnName("class_id").HasColumnType("bigint unsigned");
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("uq_teachers_user");
        builder.HasIndex(x => x.ClassId).IsUnique().HasDatabaseName("uq_teachers_homeroom_class");
        builder.HasOne(x => x.User).WithMany(x => x.Teachers)
            .HasForeignKey(x => x.UserId).HasConstraintName("fk_teachers_user")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolClass).WithMany()
            .HasForeignKey(x => x.ClassId).HasConstraintName("fk_teachers_class")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
