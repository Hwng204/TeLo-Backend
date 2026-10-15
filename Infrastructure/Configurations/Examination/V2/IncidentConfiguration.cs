using Domain.Entities.Examination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incident");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("incident_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.TestResultId).HasColumnName("test_result_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.IncidentTypeId).HasColumnName("incident_type_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.HasOne<TestResult>().WithMany()
            .HasForeignKey(x => x.TestResultId).HasConstraintName("fk_incident_test_result_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IncidentType>().WithMany()
            .HasForeignKey(x => x.IncidentTypeId).HasConstraintName("fk_incident_incident_type_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
