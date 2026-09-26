using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class MatrixDetailConfiguration : IEntityTypeConfiguration<MatrixDetail>
{
    public void Configure(EntityTypeBuilder<MatrixDetail> builder)
    {
        builder.ToTable("matrix_details", table =>
        {
            table.HasCheckConstraint("ck_matrix_details_count", "question_count > 0");
            table.HasCheckConstraint("ck_matrix_details_percentage", "percentage > 0 AND percentage <= 100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamMatrixId).HasColumnName("exam_matrix_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.LessonId).HasColumnName("lesson_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.CognitiveLevel).HasColumnName("cognitive_level").HasMaxLength(50).IsRequired();
        builder.Property(x => x.QuestionType).HasColumnName("question_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.QuestionCount).HasColumnName("question_count").HasColumnType("int unsigned").IsRequired();
        // Tỷ lệ % điểm của dòng này (0, 100], không phải điểm tuyệt đối.
        builder.Property(x => x.Percentage).HasColumnName("percentage").HasPrecision(5, 2).IsRequired();
        builder.HasIndex(x => new { x.ExamMatrixId, x.LessonId, x.CognitiveLevel, x.QuestionType })
            .IsUnique().HasDatabaseName("uq_matrix_details_cell");
        builder.HasIndex(x => x.LessonId).HasDatabaseName("idx_matrix_details_lesson");
        builder.HasOne(x => x.ExamMatrix).WithMany(x => x.Details)
            .HasForeignKey(x => x.ExamMatrixId).HasConstraintName("fk_matrix_details_matrix")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Lesson).WithMany()
            .HasForeignKey(x => x.LessonId).HasConstraintName("fk_matrix_details_lesson")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
