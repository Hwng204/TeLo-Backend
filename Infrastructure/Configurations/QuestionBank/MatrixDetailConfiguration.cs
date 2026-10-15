using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class MatrixDetailConfiguration : IEntityTypeConfiguration<MatrixDetail>
{
    public void Configure(EntityTypeBuilder<MatrixDetail> builder)
    {
        builder.ToTable("matrix_detail", table =>
        {
            table.HasCheckConstraint("ck_matrix_detail_count", "question_count > 0");
            table.HasCheckConstraint("ck_matrix_detail_allocated_score", "allocated_score > 0 AND allocated_score <= 100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("matrix_detail_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamMatrixId).HasColumnName("matrix_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.LessonId).HasColumnName("lesson_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.CognitiveLevel).HasColumnName("cognitive_level").HasMaxLength(50).IsRequired();
        builder.Property(x => x.QuestionType).HasColumnName("question_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.QuestionCount).HasColumnName("question_count").HasColumnType("int unsigned").IsRequired();
        // allocated_score giữ tỷ lệ % điểm của dòng (0, 100], không phải điểm tuyệt đối (CODE-PLAN R1).
        builder.Property(x => x.Percentage).HasColumnName("allocated_score").HasPrecision(5, 2).IsRequired();
        builder.HasIndex(x => new { x.ExamMatrixId, x.LessonId, x.CognitiveLevel, x.QuestionType })
            .IsUnique().HasDatabaseName("uq_matrix_detail_matrix_id_lesson_id_cog_level_question_type");
        builder.HasOne(x => x.ExamMatrix).WithMany(x => x.Details)
            .HasForeignKey(x => x.ExamMatrixId).HasConstraintName("fk_matrix_detail_matrix_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Lesson).WithMany()
            .HasForeignKey(x => x.LessonId).HasConstraintName("fk_matrix_detail_lesson_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
