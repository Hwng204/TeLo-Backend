using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("student");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("student_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
        builder.Property(x => x.Gender).HasColumnName("gender").HasMaxLength(10);
        builder.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20);
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("uq_student_user_id");
        builder.HasOne(x => x.User).WithMany(x => x.Students)
            .HasForeignKey(x => x.UserId).IsRequired().HasConstraintName("fk_student_user_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
