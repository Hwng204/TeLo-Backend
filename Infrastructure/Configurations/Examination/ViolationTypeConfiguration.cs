using Domain.Entities.Examination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class ViolationTypeConfiguration : IEntityTypeConfiguration<ViolationType>
{
    public void Configure(EntityTypeBuilder<ViolationType> builder)
    {
        builder.ToTable("violation_type");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("violation_type_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_violation_type_name");
    }
}
