using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class RequestCommentConfiguration : IEntityTypeConfiguration<RequestComment>
{
    public void Configure(EntityTypeBuilder<RequestComment> builder)
    {
        builder.ToTable("request_comment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("request_comment_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.RequestId).HasColumnName("request_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.CreateBy).HasColumnName("create_by").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.HasOne<UserRequest>().WithMany()
            .HasForeignKey(x => x.RequestId).HasConstraintName("fk_request_comment_request_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CreateBy).HasConstraintName("fk_request_comment_create_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
