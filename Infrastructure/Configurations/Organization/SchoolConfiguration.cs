using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.ToTable("school");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("school_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();
        builder.Property(x => x.ProvinceCode).HasColumnName("province").HasMaxLength(100);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_school_code");
    }
}
