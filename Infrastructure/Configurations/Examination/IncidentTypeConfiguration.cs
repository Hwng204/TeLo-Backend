using Domain.Entities.Examination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class IncidentTypeConfiguration : IEntityTypeConfiguration<IncidentType>
{
    public void Configure(EntityTypeBuilder<IncidentType> builder)
    {
        builder.ToTable("incident_type");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("incident_type_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_incident_type_name");
    }
}
