using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("teacher");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("teacher_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Specialization).HasColumnName("specialization").HasMaxLength(255);
        // Same text values as students: true ↔ "NAM", false ↔ "NU", any other text reads as null.
        builder.Property(x => x.Gender).HasColumnName("gender").HasMaxLength(10)
            .HasConversion(v => v.HasValue ? (v.Value ? "NAM" : "NU") : null, v => v == "NAM" ? true : v == "NU" ? false : null);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("uq_teacher_user_id");
        builder.HasOne(x => x.User).WithMany(x => x.Teachers)
            .HasForeignKey(x => x.UserId).HasConstraintName("fk_teacher_user_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
