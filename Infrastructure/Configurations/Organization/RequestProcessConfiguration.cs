using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class RequestProcessConfiguration : IEntityTypeConfiguration<RequestProcess>
{
    public void Configure(EntityTypeBuilder<RequestProcess> builder)
    {
        builder.ToTable("request_process");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("request_process_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_request_process_name");
    }
}
