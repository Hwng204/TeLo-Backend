using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class GradeLevelConfiguration : IEntityTypeConfiguration<GradeLevel>
{
    public void Configure(EntityTypeBuilder<GradeLevel> builder)
    {
        builder.ToTable("grade_levels");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_grade_levels_name");
        builder.HasData(
            new GradeLevel { Id = 1, Name = "Khối 1", Status = "ACTIVE" },
            new GradeLevel { Id = 2, Name = "Khối 2", Status = "ACTIVE" },
            new GradeLevel { Id = 3, Name = "Khối 3", Status = "ACTIVE" },
            new GradeLevel { Id = 4, Name = "Khối 4", Status = "ACTIVE" },
            new GradeLevel { Id = 5, Name = "Khối 5", Status = "ACTIVE" });
    }
}
