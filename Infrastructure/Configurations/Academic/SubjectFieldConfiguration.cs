using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class SubjectFieldConfiguration : IEntityTypeConfiguration<SubjectField>
{
    public void Configure(EntityTypeBuilder<SubjectField> builder)
    {
        builder.ToTable("subject_fields");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).HasDefaultValue("ACTIVE").IsRequired();
        builder.HasIndex(x => new { x.SubjectId, x.Name }).IsUnique().HasDatabaseName("uq_subject_fields_name");
        builder.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).HasConstraintName("fk_subject_fields_subject")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
