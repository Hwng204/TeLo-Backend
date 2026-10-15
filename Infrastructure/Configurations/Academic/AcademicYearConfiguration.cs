using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("academic_year", table =>
            table.HasCheckConstraint("ck_academic_year_status", "status IN ('DRAFT', 'ACTIVE', 'CLOSED')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("academic_year_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("DRAFT").IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_academic_year_name");
    }
}
