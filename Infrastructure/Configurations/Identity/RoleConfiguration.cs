using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_roles_code");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE");
        builder.Property(x => x.SchoolId).HasColumnName("school_id");
        builder.Property(x => x.SchoolBranchId).HasColumnName("school_branch_id");
        builder.Property(x => x.IsSystem).HasColumnName("is_system");
        builder.Property(x => x.Version).HasColumnName("version").HasDefaultValue(1u).IsConcurrencyToken();
        builder.Property(x => x.UsedAt).HasColumnName("used_at").HasColumnType("datetime(6)");
        builder.HasOne(x => x.School).WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SchoolBranch).WithMany().HasForeignKey(x => x.SchoolBranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Status, x.SchoolId, x.SchoolBranchId });
    }
}
