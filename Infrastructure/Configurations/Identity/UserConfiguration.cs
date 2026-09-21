using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.Property(x => x.SecurityVersion).HasColumnName("security_version").HasDefaultValue(1u).IsConcurrencyToken();
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(x => x.SchoolBranchId).HasColumnName("school_branch_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.MoetIdentifier).HasColumnName("moet_identifier").HasMaxLength(100);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.HasIndex(x => x.Username).IsUnique().HasDatabaseName("uq_users_username");
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_users_email");
        builder.HasIndex(x => x.MoetIdentifier).IsUnique().HasDatabaseName("uq_users_moet_identifier");
        builder.HasIndex(x => new { x.SchoolBranchId, x.Status }).HasDatabaseName("idx_users_branch_status");
        builder.HasOne(x => x.SchoolBranch).WithMany()
            .HasForeignKey(x => x.SchoolBranchId).HasConstraintName("fk_users_school_branch")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
