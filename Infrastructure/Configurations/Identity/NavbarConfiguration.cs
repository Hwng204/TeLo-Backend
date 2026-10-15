using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class NavbarConfiguration : IEntityTypeConfiguration<Navbar>
{
    public void Configure(EntityTypeBuilder<Navbar> builder)
    {
        builder.ToTable("navbar");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("navbar_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ModuleId).HasColumnName("module_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.ParentId).HasColumnName("parent_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasColumnType("int unsigned").HasDefaultValue(0u).IsRequired();
        builder.Property(x => x.UrlPath).HasColumnName("url_path").HasMaxLength(500);
        builder.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_navbar_code");
        builder.HasIndex(x => new { x.ModuleId, x.ParentId, x.DisplayOrder })
            .HasDatabaseName("idx_navbars_module_parent_order");
        builder.HasOne(x => x.Module).WithMany(x => x.Navbars)
            .HasForeignKey(x => x.ModuleId).HasConstraintName("fk_navbar_module_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Parent).WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId).HasConstraintName("fk_navbar_parent_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
