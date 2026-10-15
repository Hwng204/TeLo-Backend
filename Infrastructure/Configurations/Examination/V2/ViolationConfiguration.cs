using Domain.Entities.Examination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class ViolationConfiguration : IEntityTypeConfiguration<Violation>
{
    public void Configure(EntityTypeBuilder<Violation> builder)
    {
        builder.ToTable("violation");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("violation_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.TestResultId).HasColumnName("test_result_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.ViolationTypeId).HasColumnName("violation_type_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Decision).HasColumnName("decision").HasMaxLength(50);
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.HasOne<TestResult>().WithMany()
            .HasForeignKey(x => x.TestResultId).HasConstraintName("fk_violation_test_result_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ViolationType>().WithMany()
            .HasForeignKey(x => x.ViolationTypeId).HasConstraintName("fk_violation_violation_type_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
