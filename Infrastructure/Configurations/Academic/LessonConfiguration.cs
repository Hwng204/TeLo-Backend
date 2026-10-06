using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("lessons");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ChapterId).HasColumnName("chapter_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(32)
            .UseCollation(ChapterConfiguration.CodeTitleCollation).IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255)
            .UseCollation(ChapterConfiguration.CodeTitleCollation).IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("longtext");
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").HasColumnType("int unsigned").IsRequired();

        // Mã và tên bài không trùng trong cùng chương.
        builder.HasIndex(x => new { x.ChapterId, x.Code }).IsUnique().HasDatabaseName("uq_lessons_code");
        builder.HasIndex(x => new { x.ChapterId, x.Title }).IsUnique().HasDatabaseName("uq_lessons_title");

        // Xoá chương thì xoá bài; bài đang được ma trận/nhiệm vụ dùng vẫn bị khoá ngoại RESTRICT chặn.
        builder.HasOne(x => x.Chapter).WithMany(x => x.Lessons)
            .HasForeignKey(x => x.ChapterId).HasConstraintName("fk_lessons_chapter")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
