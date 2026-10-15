using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class ExamSetConfiguration : IEntityTypeConfiguration<ExamSet>
{
    public void Configure(EntityTypeBuilder<ExamSet> builder)
    {
        builder.ToTable("exam_set");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("exam_set_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamMatrixId).HasColumnName("matrix_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.DurationMinutes).HasColumnName("duration_minutes").HasColumnType("int unsigned").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.ApprovedByUserId).HasColumnName("review_by_user_id").HasColumnType("bigint unsigned");
        builder.HasOne(x => x.ExamMatrix).WithMany()
            .HasForeignKey(x => x.ExamMatrixId).HasConstraintName("fk_exam_set_matrix_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Task).WithMany()
            .HasForeignKey(x => x.TaskId).HasConstraintName("fk_exam_set_task_id")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.ApprovedByUser).WithMany()
            .HasForeignKey(x => x.ApprovedByUserId).HasConstraintName("fk_exam_set_review_by_user_id")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
