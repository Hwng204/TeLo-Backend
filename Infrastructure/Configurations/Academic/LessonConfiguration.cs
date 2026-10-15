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
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(255)
            .UseCollation(ChapterConfiguration.TitleCollation).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();

        // Tên bài không trùng trong cùng chương.
        builder.HasIndex(x => new { x.ChapterId, x.Title }).IsUnique().HasDatabaseName("uq_lesson_chapter_id_title");

        // Xoá chương thì xoá bài; bài đang được ma trận/nhiệm vụ dùng vẫn bị khoá ngoại RESTRICT chặn.
        builder.HasOne(x => x.Chapter).WithMany(x => x.Lessons)
            .HasForeignKey(x => x.ChapterId).HasConstraintName("fk_lesson_chapter_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
