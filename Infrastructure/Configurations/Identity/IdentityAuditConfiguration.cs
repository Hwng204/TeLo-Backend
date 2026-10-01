using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class IdentityAuditConfiguration : IEntityTypeConfiguration<IdentityAudit>
{
    public void Configure(EntityTypeBuilder<IdentityAudit> builder)
    {
        builder.ToTable("identity_audits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(32);
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(32);
        builder.Property(x => x.Data).HasColumnName("data").HasColumnType("longtext");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        builder.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
    }
}
