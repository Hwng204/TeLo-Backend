using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class SchoolBranchConfiguration : IEntityTypeConfiguration<SchoolBranch>
{
    public void Configure(EntityTypeBuilder<SchoolBranch> builder)
    {
        builder.ToTable("branch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("branch_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(x => x.Logo).HasColumnName("logo").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();

        builder.HasIndex(x => new { x.SchoolId, x.Code })
            .IsUnique().HasDatabaseName("uq_branch_school_id_code");
        builder.HasOne(x => x.School)
            .WithMany(x => x.Branches)
            .HasForeignKey(x => x.SchoolId)
            .HasConstraintName("fk_branch_school_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
