using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("academic_years", table =>
        {
            table.HasCheckConstraint("ck_academic_years_dates", "end_date > start_date");
            table.HasCheckConstraint("ck_academic_years_status", "status IN ('DRAFT', 'ACTIVE', 'CLOSED')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(64);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("DRAFT").IsRequired();
        builder.Property(x => x.ActiveSystemKey)
            .HasColumnName("active_system_key")
            .HasMaxLength(16)
            .HasComputedColumnSql(
                "CASE WHEN status = 'ACTIVE' THEN 'SYSTEM' ELSE NULL END",
                stored: true);
        builder.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1u).IsConcurrencyToken();
        builder.HasIndex(x => x.Name)
            .HasDatabaseName("ix_academic_years_name");
        builder.HasIndex(x => x.Code)
            .IsUnique().HasDatabaseName("uq_academic_years_code");
        builder.HasIndex(x => x.ActiveSystemKey)
            .IsUnique().HasDatabaseName("uq_academic_years_active_system");
    }
}
