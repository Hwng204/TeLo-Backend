using Domain.Entities.Examination;
using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class TestResultDetailConfiguration : IEntityTypeConfiguration<TestResultDetail>
{
    public void Configure(EntityTypeBuilder<TestResultDetail> builder)
    {
        builder.ToTable("test_result_detail");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("test_result_detail_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.TestResultId).HasColumnName("test_result_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.QuestionId).HasColumnName("question_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.SelectedOptionKey).HasColumnName("selected_option_key").HasMaxLength(5);
        builder.Property(x => x.ScoreAwarded).HasColumnName("score_awarded").HasColumnType("decimal(5,2)").HasDefaultValue(0m).IsRequired();
        builder.HasIndex(x => new { x.TestResultId, x.QuestionId }).IsUnique().HasDatabaseName("uq_test_result_detail_test_result_id_question_id");
        builder.HasOne<TestResult>().WithMany()
            .HasForeignKey(x => x.TestResultId).HasConstraintName("fk_test_result_detail_test_result_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Question>().WithMany()
            .HasForeignKey(x => x.QuestionId).HasConstraintName("fk_test_result_detail_question_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
