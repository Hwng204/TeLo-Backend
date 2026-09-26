using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class StudentImportBatchConfiguration : IEntityTypeConfiguration<StudentImportBatch>
{
    public void Configure(EntityTypeBuilder<StudentImportBatch> builder)
    {
        builder.ToTable("student_import_batches", table =>
        {
            table.HasCheckConstraint(
                "ck_student_import_batches_source", "source IN ('ADMIN', 'SCHOOL')");
            table.HasCheckConstraint(
                "ck_student_import_batches_status",
                "status IN ('DRAFT', 'SUBMITTED', 'REJECTED', 'APPLIED', 'CANCELLED')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.SchoolId).HasColumnName("school_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.AcademicYearId).HasColumnName("academic_year_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32)
            .HasDefaultValue(StudentImportBatchStatusCodes.Draft).IsRequired();
        builder.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.FileContent).HasColumnName("file_content").HasColumnType("longblob").IsRequired();
        builder.Property(x => x.FileSize).HasColumnName("file_size").HasColumnType("int unsigned").IsRequired();
        builder.Property(x => x.TotalRows).HasColumnName("total_rows").HasColumnType("int unsigned").IsRequired();
        builder.Property(x => x.ValidRows).HasColumnName("valid_rows").HasColumnType("int unsigned").IsRequired();
        builder.Property(x => x.InvalidRows).HasColumnName("invalid_rows").HasColumnType("int unsigned").IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("datetime(6)");
        builder.Property(x => x.ReviewComment).HasColumnName("review_comment").HasMaxLength(1000);
        builder.Property(x => x.AppliedAt).HasColumnName("applied_at").HasColumnType("datetime(6)");

        builder.HasIndex(x => new { x.SchoolId, x.Status }).HasDatabaseName("idx_student_import_batches_school_status");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("idx_student_import_batches_creator");
        builder.HasIndex(x => x.ReviewedByUserId).HasDatabaseName("idx_student_import_batches_reviewer");

        builder.HasOne(x => x.School).WithMany()
            .HasForeignKey(x => x.SchoolId).HasConstraintName("fk_student_import_batches_school")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AcademicYear).WithMany()
            .HasForeignKey(x => x.AcademicYearId).HasConstraintName("fk_student_import_batches_year")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany()
            .HasForeignKey(x => x.CreatedByUserId).HasConstraintName("fk_student_import_batches_creator")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany()
            .HasForeignKey(x => x.ReviewedByUserId).HasConstraintName("fk_student_import_batches_reviewer")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
