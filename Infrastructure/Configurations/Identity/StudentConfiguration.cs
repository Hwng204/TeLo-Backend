using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students", table => table.HasCheckConstraint(
            "ck_students_status",
            "status IN ('ACTIVE', 'TEMPORARY_LEAVE', 'TRANSFERRED', 'INACTIVE')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64).IsRequired();
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
        builder.Property(x => x.Gender).HasColumnName("gender").HasMaxLength(20);
        builder.Property(x => x.AdmissionDate).HasColumnName("admission_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32)
            .HasDefaultValue(StudentStatusCodes.Active).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("uq_students_user");
        // Generated: the code while the student is not INACTIVE. Uniqueness over it (NULLs never
        // collide) frees the code of a deleted student for re-import at another school.
        builder.Property(x => x.ActiveCode).HasColumnName("active_code").HasMaxLength(64)
            .HasComputedColumnSql("CASE WHEN status <> 'INACTIVE' THEN code ELSE NULL END", stored: true);
        builder.HasIndex(x => x.ActiveCode).IsUnique().HasDatabaseName("uq_students_active_code");
        builder.HasIndex(x => x.Code).HasDatabaseName("idx_students_code");
        builder.HasIndex(x => x.Status).HasDatabaseName("idx_students_status");
        builder.HasOne(x => x.User).WithMany(x => x.Students)
            .HasForeignKey(x => x.UserId).HasConstraintName("fk_students_user")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
