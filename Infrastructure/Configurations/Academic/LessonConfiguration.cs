using Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Academic;

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("lesson");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("lesson_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ChapterId).HasColumnName("chapter_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Number).HasColumnName("number").IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255)
            .UseCollation(ChapterConfiguration.TitleCollation).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property<byte?>(ChapterConfiguration.ActiveFlag).HasColumnName("active_flag")
            .HasComputedColumnSql(ChapterConfiguration.ActiveFlagSql, stored: false);

        // Số và tên bài không trùng giữa các bài ACTIVE trong cùng chương; bài lưu trữ không tính.
        builder.HasIndex(nameof(Lesson.ChapterId), nameof(Lesson.Number), ChapterConfiguration.ActiveFlag)
            .IsUnique().HasDatabaseName("uq_lesson_active_number");
        builder.HasIndex(nameof(Lesson.ChapterId), nameof(Lesson.Title), ChapterConfiguration.ActiveFlag)
            .IsUnique().HasDatabaseName("uq_lesson_active_title");

        // Xoá chương thì xoá bài; bài đang được ma trận/nhiệm vụ dùng vẫn bị khoá ngoại RESTRICT chặn.
        builder.HasOne(x => x.Chapter).WithMany(x => x.Lessons)
            .HasForeignKey(x => x.ChapterId).HasConstraintName("fk_lesson_chapter_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
