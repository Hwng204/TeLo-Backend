using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Identity;

public sealed class StudentImportRowConfiguration : IEntityTypeConfiguration<StudentImportRow>
{
    public void Configure(EntityTypeBuilder<StudentImportRow> builder)
    {
        builder.ToTable("student_import_rows");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.BatchId).HasColumnName("batch_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.RowNumber).HasColumnName("row_number").HasColumnType("int unsigned").IsRequired();
        builder.Property(x => x.RawCode).HasColumnName("raw_code").HasMaxLength(255).IsRequired();
        builder.Property(x => x.RawFullName).HasColumnName("raw_full_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.RawDateOfBirth).HasColumnName("raw_date_of_birth").HasMaxLength(255).IsRequired();
        builder.Property(x => x.RawGender).HasColumnName("raw_gender").HasMaxLength(255).IsRequired();
        builder.Property(x => x.RawAdmissionDate).HasColumnName("raw_admission_date").HasMaxLength(255).IsRequired();
        builder.Property(x => x.RawClassCode).HasColumnName("raw_class_code").HasMaxLength(255).IsRequired();
        builder.Property(x => x.ResolvedSchoolClassId).HasColumnName("resolved_school_class_id").HasColumnType("bigint unsigned");
        builder.Property(x => x.IsValid).HasColumnName("is_valid").HasColumnType("tinyint(1)").IsRequired();
        builder.Property(x => x.ErrorJson).HasColumnName("error_json").HasMaxLength(2000);
        builder.Property(x => x.CreatedStudentId).HasColumnName("created_student_id").HasColumnType("bigint unsigned");

        builder.HasIndex(x => new { x.BatchId, x.RowNumber })
            .IsUnique().HasDatabaseName("uq_student_import_rows_batch_row");
        builder.HasIndex(x => new { x.BatchId, x.IsValid }).HasDatabaseName("idx_student_import_rows_batch_valid");
        builder.HasIndex(x => x.ResolvedSchoolClassId).HasDatabaseName("idx_student_import_rows_class");
        builder.HasIndex(x => x.CreatedStudentId).HasDatabaseName("idx_student_import_rows_student");

        builder.HasOne(x => x.Batch).WithMany(x => x.Rows)
            .HasForeignKey(x => x.BatchId).HasConstraintName("fk_student_import_rows_batch")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ResolvedSchoolClass).WithMany()
            .HasForeignKey(x => x.ResolvedSchoolClassId).HasConstraintName("fk_student_import_rows_class")
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.CreatedStudent).WithMany()
            .HasForeignKey(x => x.CreatedStudentId).HasConstraintName("fk_student_import_rows_student")
            .OnDelete(DeleteBehavior.SetNull);
    }
}
