using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permission");
        builder.HasKey(x => new { x.RoleId, x.NavbarId, x.SchoolId, x.Value });
        builder.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.NavbarId).HasColumnName("navbar_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SchoolId).HasColumnName("school_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Value).HasColumnName("permission").HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.NavbarId).HasDatabaseName("idx_permissions_navbar");
        builder.HasOne(x => x.Role).WithMany(x => x.Permissions)
            .HasForeignKey(x => x.RoleId).HasConstraintName("fk_permission_role_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Navbar).WithMany(x => x.Permissions)
            .HasForeignKey(x => x.NavbarId).HasConstraintName("fk_permission_navbar_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Organization.School>().WithMany()
            .HasForeignKey(x => x.SchoolId).HasConstraintName("fk_permission_school_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
