using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("user_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Username).HasColumnName("username").HasMaxLength(255).IsRequired();
        builder.Property(x => x.SchoolBranchId).HasColumnName("branch_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.PasswordHash).HasColumnName("password").HasMaxLength(255).IsRequired();
        builder.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.MoetIdentifier).HasColumnName("moet_identifier").HasMaxLength(50);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();
        builder.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)")
            .HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        // Extra column kept (decision 2): bumping it revokes issued tokens.
        builder.Property(x => x.SecurityVersion).HasColumnName("security_version").HasDefaultValue(1u).IsConcurrencyToken();
        builder.HasIndex(x => x.Username).IsUnique().HasDatabaseName("uq_user_username");
        builder.HasIndex(x => x.MoetIdentifier).IsUnique().HasDatabaseName("uq_user_moet_identifier");
        builder.HasIndex(x => new { x.SchoolBranchId, x.Status }).HasDatabaseName("idx_users_branch_status");
        builder.HasOne(x => x.SchoolBranch).WithMany()
            .HasForeignKey(x => x.SchoolBranchId).HasConstraintName("fk_user_branch_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
