using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    // Không phân biệt hoa thường nhưng phân biệt dấu, để "Hình" và "Hinh" không bị coi là trùng.
    public const string CodeTitleCollation = "utf8mb4_0900_as_ci";

    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.ToTable("chapters");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolBranchId).HasColumnName("school_branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.FieldId).HasColumnName("field_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(32).UseCollation(CodeTitleCollation).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).UseCollation(CodeTitleCollation).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasColumnType("int unsigned").IsRequired();

        // Mã và tên chương không trùng trong cùng phân hiệu, khối và lĩnh vực.
        builder.HasIndex(x => new { x.SchoolBranchId, x.GradeLevelId, x.FieldId, x.Code })
            .IsUnique().HasDatabaseName("uq_chapters_code");
        builder.HasIndex(x => new { x.SchoolBranchId, x.GradeLevelId, x.FieldId, x.Title })
            .IsUnique().HasDatabaseName("uq_chapters_title");
        builder.HasIndex(x => x.GradeLevelId).HasDatabaseName("idx_chapters_grade");
        builder.HasIndex(x => x.FieldId).HasDatabaseName("idx_chapters_field");

        builder.HasOne(x => x.SchoolBranch).WithMany()
            .HasForeignKey(x => x.SchoolBranchId).HasConstraintName("fk_chapters_branch")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GradeLevel).WithMany()
            .HasForeignKey(x => x.GradeLevelId).HasConstraintName("fk_chapters_grade")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Field).WithMany()
            .HasForeignKey(x => x.FieldId).HasConstraintName("fk_chapters_field")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
