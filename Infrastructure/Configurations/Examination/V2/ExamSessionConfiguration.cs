using Domain.Entities.Examination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class ExamSessionConfiguration : IEntityTypeConfiguration<ExamSession>
{
    public void Configure(EntityTypeBuilder<ExamSession> builder)
    {
        builder.ToTable("exam_session");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("exam_session_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamId).HasColumnName("exam_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.StartAt).HasColumnName("start_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.EndAt).HasColumnName("end_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.ExamId, x.Code }).IsUnique().HasDatabaseName("uq_exam_session_exam_id_code");
        builder.HasOne<Exam>().WithMany()
            .HasForeignKey(x => x.ExamId).HasConstraintName("fk_exam_session_exam_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
