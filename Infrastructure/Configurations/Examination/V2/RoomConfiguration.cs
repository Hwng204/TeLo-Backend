using Domain.Entities.Examination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("room");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("room_id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamSessionId).HasColumnName("exam_session_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Capacity).HasColumnName("capacity").HasColumnType("int unsigned").IsRequired();
        builder.Property(x => x.AccessCode).HasColumnName("access_code").HasMaxLength(50);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.ExamSessionId, x.Code }).IsUnique().HasDatabaseName("uq_room_exam_session_id_code");
        builder.HasOne<ExamSession>().WithMany()
            .HasForeignKey(x => x.ExamSessionId).HasConstraintName("fk_room_exam_session_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
