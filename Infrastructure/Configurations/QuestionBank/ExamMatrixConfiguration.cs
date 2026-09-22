using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class ExamMatrixConfiguration : IEntityTypeConfiguration<ExamMatrix>
{
    public void Configure(EntityTypeBuilder<ExamMatrix> builder)
    {
        builder.ToTable("exam_matrices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.SemesterId).HasColumnName("semester_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.AcademicContextId).HasColumnName("academic_context_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.RejectComment).HasColumnName("reject_comment").HasMaxLength(1000);
        builder.Property(x => x.RejectedByUserId).HasColumnName("rejected_by_user_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.RejectedAt).HasColumnName("rejected_at").HasColumnType("datetime(6)");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.ApprovedByUserId).HasColumnName("approved_by_user_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.ApprovedAt).HasColumnName("approved_at").HasColumnType("datetime(6)");
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_exam_matrices_code");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("idx_exam_matrices_creator");
        builder.HasIndex(x => x.ApprovedByUserId).HasDatabaseName("idx_exam_matrices_approver");
        builder.HasIndex(x => x.TaskId).IsUnique().HasDatabaseName("uq_exam_matrices_task");
        builder.HasIndex(x => x.RejectedByUserId).HasDatabaseName("idx_exam_matrices_rejecter");
        builder.HasIndex(x => x.AcademicContextId).HasDatabaseName("idx_exam_matrices_context");
        builder.HasIndex(x => x.SemesterId).HasDatabaseName("idx_exam_matrices_semester");
        builder.HasOne(x => x.Task).WithMany()
            .HasForeignKey(x => x.TaskId).HasConstraintName("fk_exam_matrices_task")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Semester).WithMany()
            .HasForeignKey(x => x.SemesterId).HasConstraintName("fk_exam_matrices_semester")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.RejectedByUserId).HasConstraintName("fk_exam_matrices_rejecter")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CreatedByUserId).HasConstraintName("fk_exam_matrices_creator")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.ApprovedByUserId).HasConstraintName("fk_exam_matrices_approver")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.AcademicContext).WithMany()
            .HasForeignKey(x => x.AcademicContextId).HasConstraintName("fk_exam_matrices_context")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
