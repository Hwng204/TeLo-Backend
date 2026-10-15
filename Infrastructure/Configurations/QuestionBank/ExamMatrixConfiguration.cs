using Domain.Entities.Identity;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class ExamMatrixConfiguration : IEntityTypeConfiguration<ExamMatrix>
{
    public void Configure(EntityTypeBuilder<ExamMatrix> builder)
    {
        builder.ToTable("matrix", table =>
        {
            table.HasCheckConstraint("ck_matrix_total_score", "total_score > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("matrix_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.BranchId).HasColumnName("branch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("create_by").HasColumnType("bigint unsigned").IsRequired();
        // Số nguyên dương do người lập tự đặt (≤ 999); điểm mỗi ô = total_score * matrix_detail.allocated_score / 100.
        builder.Property(x => x.TotalScore).HasColumnName("total_score").HasConversion<decimal>()
            .HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.GradeLevelId).HasColumnName("grade_level_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("bigint unsigned");
        builder.HasIndex(x => x.TaskId).IsUnique().HasDatabaseName("uq_matrix_task_id");
        builder.HasOne(x => x.Branch).WithMany()
            .HasForeignKey(x => x.BranchId).HasConstraintName("fk_matrix_branch_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(x => x.CreatedByUserId).HasConstraintName("fk_matrix_create_by")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Subject).WithMany()
            .HasForeignKey(x => x.SubjectId).HasConstraintName("fk_matrix_subject_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.GradeLevel).WithMany()
            .HasForeignKey(x => x.GradeLevelId).HasConstraintName("fk_matrix_grade_level_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Task).WithMany()
            .HasForeignKey(x => x.TaskId).HasConstraintName("fk_matrix_task_id")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
