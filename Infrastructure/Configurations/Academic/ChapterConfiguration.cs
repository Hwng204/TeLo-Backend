using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    // TiDB không có utf8mb4_0900_as_ci nên dùng ai_ci: không phân biệt hoa thường lẫn dấu ("Hình" trùng "Hinh").
    // shortcut: import (Fold) vẫn phân biệt dấu, tên chỉ khác dấu sẽ bị khoá unique chặn; sửa Fold nếu nhóm cần.
    public const string TitleCollation = "utf8mb4_0900_ai_ci";

    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.ToTable("chapter");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("chapter_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolBranchId).HasColumnName("branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).UseCollation(TitleCollation).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();

        // Tên chương không trùng trong cùng phân hiệu, môn học và khối.
        builder.HasIndex(x => new { x.SchoolBranchId, x.SubjectId, x.GradeLevelId, x.Title })
            .IsUnique().HasDatabaseName("uq_chapter_branch_id_subject_id_grade_level_id_title");
        builder.HasIndex(x => x.GradeLevelId).HasDatabaseName("idx_chapters_grade");

        builder.HasOne(x => x.SchoolBranch).WithMany()
            .HasForeignKey(x => x.SchoolBranchId).HasConstraintName("fk_chapter_branch_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).HasConstraintName("fk_chapter_subject_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GradeLevel).WithMany()
            .HasForeignKey(x => x.GradeLevelId).HasConstraintName("fk_chapter_grade_level_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
