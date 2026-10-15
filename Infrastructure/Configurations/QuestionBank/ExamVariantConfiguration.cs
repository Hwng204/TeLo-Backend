using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class ExamVariantConfiguration : IEntityTypeConfiguration<ExamVariant>
{
    public void Configure(EntityTypeBuilder<ExamVariant> builder)
    {
        builder.ToTable("exam_variant");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("exam_variant_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamSetId).HasColumnName("exam_set_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.VariantCode).HasColumnName("variant_code").HasMaxLength(20).IsRequired();
        builder.Property(x => x.QuestionOrder).HasColumnName("question_order").HasColumnType("json").IsRequired();
        builder.HasIndex(x => new { x.ExamSetId, x.VariantCode })
            .IsUnique().HasDatabaseName("uq_exam_variant_exam_set_id_variant_code");
        builder.HasOne(x => x.ExamSet).WithMany(x => x.Variants)
            .HasForeignKey(x => x.ExamSetId).HasConstraintName("fk_exam_variant_exam_set_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
