using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class SchoolBranchConfiguration : IEntityTypeConfiguration<SchoolBranch>
{
    public void Configure(EntityTypeBuilder<SchoolBranch> builder)
    {
        builder.ToTable("school_branches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(x => x.Logo).HasColumnName("logo").HasMaxLength(1000);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();

        builder.HasIndex(x => new { x.SchoolId, x.Code })
            .IsUnique().HasDatabaseName("uq_school_branches_school_code");
        builder.HasAlternateKey(x => new { x.Id, x.SchoolId })
            .HasName("uq_school_branches_id_school");
        builder.HasOne(x => x.School)
            .WithMany(x => x.Branches)
            .HasForeignKey(x => x.SchoolId)
            .HasConstraintName("fk_school_branches_school")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
