using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Organization;

public sealed class RequestAttachmentConfiguration : IEntityTypeConfiguration<RequestAttachment>
{
    public void Configure(EntityTypeBuilder<RequestAttachment> builder)
    {
        builder.ToTable("request_attachment");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("request_attachment_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.RequestId).HasColumnName("request_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.RequestStepId).HasColumnName("request_step_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Path).HasColumnName("path").HasMaxLength(500).IsRequired();
        builder.Property(x => x.UploadedBy).HasColumnName("uploaded_by").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.UploadedAt).HasColumnName("uploaded_at").HasColumnType("datetime(6)").HasDefaultValueSql("CURRENT_TIMESTAMP(6)").IsRequired();
        builder.HasOne<UserRequest>().WithMany()
            .HasForeignKey(x => x.RequestId).HasConstraintName("fk_request_attachment_request_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RequestStep>().WithMany()
            .HasForeignKey(x => x.RequestStepId).HasConstraintName("fk_request_attachment_request_step_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.UploadedBy).HasConstraintName("fk_request_attachment_uploaded_by")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
