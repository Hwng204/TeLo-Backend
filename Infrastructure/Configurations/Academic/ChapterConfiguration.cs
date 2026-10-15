using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class ChapterConfiguration : IEntityTypeConfiguration<Chapter>
{
    // TiDB không có utf8mb4_0900_as_ci nên dùng ai_ci: không phân biệt hoa thường lẫn dấu ("Hình" trùng "Hinh").
    // shortcut: import (Fold) vẫn phân biệt dấu, tên chỉ khác dấu sẽ bị khoá unique chặn; sửa Fold nếu nhóm cần.
    public const string TitleCollation = "utf8mb4_0900_ai_ci";

    // 1 for ACTIVE rows, NULL otherwise: unique indexes ending in it only bind ACTIVE rows, since MySQL and TiDB
    // let NULLs repeat. VIRTUAL because TiDB cannot add a STORED generated column with ALTER TABLE.
    public const string ActiveFlag = "ActiveFlag";
    public const string ActiveFlagSql = "(case when `status` = 'ACTIVE' then 1 end)";

    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.ToTable("chapter");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("chapter_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolBranchId).HasColumnName("branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Number).HasColumnName("number").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).UseCollation(TitleCollation).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property<byte?>(ActiveFlag).HasColumnName("active_flag").HasComputedColumnSql(ActiveFlagSql, stored: false);

        // Số và tên chương không trùng giữa các chương ACTIVE cùng phân hiệu, môn học và khối; chương lưu trữ không tính.
        builder.HasIndex(nameof(Chapter.SchoolBranchId), nameof(Chapter.SubjectId), nameof(Chapter.GradeLevelId),
                nameof(Chapter.Number), ActiveFlag)
            .IsUnique().HasDatabaseName("uq_chapter_active_number");
        builder.HasIndex(nameof(Chapter.SchoolBranchId), nameof(Chapter.SubjectId), nameof(Chapter.GradeLevelId),
                nameof(Chapter.Title), ActiveFlag)
            .IsUnique().HasDatabaseName("uq_chapter_active_title");
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
