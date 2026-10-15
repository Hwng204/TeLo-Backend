using Domain.Entities.QuestionBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.QuestionBank;

public sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ToTable("answer");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("answer_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.QuestionId).HasColumnName("question_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.IsCorrect).HasColumnName("is_correct").HasColumnType("tinyint(1)").HasDefaultValue(false).IsRequired();
        builder.Property(x => x.OptionKey).HasColumnName("option_key").HasMaxLength(5).IsRequired();
        builder.HasIndex(x => new { x.QuestionId, x.OptionKey })
            .IsUnique().HasDatabaseName("uq_answer_question_id_option_key");
        builder.HasOne(x => x.Question).WithMany(x => x.Options)
            .HasForeignKey(x => x.QuestionId).HasConstraintName("fk_answer_question_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
