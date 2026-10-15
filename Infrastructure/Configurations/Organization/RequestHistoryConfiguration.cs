using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class RequestHistoryConfiguration : IEntityTypeConfiguration<RequestHistory>
{
    public void Configure(EntityTypeBuilder<RequestHistory> builder)
    {
        builder.ToTable("request_history");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("request_history_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.RequestId).HasColumnName("request_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.RequestStepId).HasColumnName("request_step_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Message).HasColumnName("message").HasColumnType("text");
        builder.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(50);
        builder.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(50).IsRequired();
        builder.Property(x => x.HandleBy).HasColumnName("handle_by").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.HasOne<UserRequest>().WithMany()
            .HasForeignKey(x => x.RequestId).HasConstraintName("fk_request_history_request_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RequestStep>().WithMany()
            .HasForeignKey(x => x.RequestStepId).HasConstraintName("fk_request_history_request_step_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.HandleBy).HasConstraintName("fk_request_history_handle_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
