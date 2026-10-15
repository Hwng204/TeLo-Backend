using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> builder)
    {
        builder.ToTable("semester", table => table.HasCheckConstraint("ck_semester_code", "code IN ('1','2')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("semester_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.AcademicYearId).HasColumnName("academic_year_id").HasColumnType("bigint unsigned").IsRequired();
        // Order (1/2) is stored in v2's varchar `code` column as "1"/"2".
        builder.Property(x => x.Order).HasColumnName("code").HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.HasIndex(x => new { x.AcademicYearId, x.Order })
            .IsUnique().HasDatabaseName("uq_semester_academic_year_id_code");
        builder.HasOne(x => x.AcademicYear).WithMany(x => x.Semesters)
            .HasForeignKey(x => x.AcademicYearId).HasConstraintName("fk_semester_academic_year_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
