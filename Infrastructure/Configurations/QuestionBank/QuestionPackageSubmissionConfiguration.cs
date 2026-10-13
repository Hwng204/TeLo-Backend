using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class QuestionPackageSubmissionConfiguration : IEntityTypeConfiguration<QuestionPackageSubmission>
{
    public void Configure(EntityTypeBuilder<QuestionPackageSubmission> b)
    {
        b.ToTable("question_package_submissions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned");
        b.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("bigint unsigned");
        b.Property(x => x.Number).HasColumnName("number");
        b.Property(x => x.Version).HasColumnName("version");
        b.Property(x => x.SubmittedAt).HasColumnName("submitted_at").HasColumnType("datetime(6)");
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
        b.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("longtext");
        b.Property(x => x.NotesJson).HasColumnName("notes_json").HasColumnType("longtext");
        b.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
        b.Property(x => x.ReviewedAt).HasColumnName("reviewed_at").HasColumnType("datetime(6)");
        b.Property(x => x.ReviewedByUserId).HasColumnName("reviewed_by_user_id").HasColumnType("bigint unsigned");
        b.HasIndex(x => new { x.TaskId, x.Number }).IsUnique();
        b.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Domain.Entities.Identity.User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
