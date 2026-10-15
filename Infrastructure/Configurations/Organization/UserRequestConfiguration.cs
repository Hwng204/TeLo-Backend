using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class UserRequestConfiguration : IEntityTypeConfiguration<UserRequest>
{
    public void Configure(EntityTypeBuilder<UserRequest> builder)
    {
        builder.ToTable("request");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("request_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.RequestProcessId).HasColumnName("request_process_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasColumnType("text");
        builder.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        builder.HasOne<RequestProcess>().WithMany()
            .HasForeignKey(x => x.RequestProcessId).HasConstraintName("fk_request_request_process_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CreatedBy).HasConstraintName("fk_request_created_by")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SchoolBranch>().WithMany()
            .HasForeignKey(x => x.BranchId).HasConstraintName("fk_request_branch_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
