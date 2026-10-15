using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class RequestStepConfiguration : IEntityTypeConfiguration<RequestStep>
{
    public void Configure(EntityTypeBuilder<RequestStep> builder)
    {
        builder.ToTable("request_step");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("request_step_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.RequestProcessId).HasColumnName("request_process_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.StepOrder).HasColumnName("step_order").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.HandlerRole).HasColumnName("handler_role").HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.HasIndex(x => new { x.RequestProcessId, x.StepOrder })
            .IsUnique().HasDatabaseName("uq_request_step_request_process_id_step_order");
        builder.HasOne<RequestProcess>().WithMany()
            .HasForeignKey(x => x.RequestProcessId).HasConstraintName("fk_request_step_request_process_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
