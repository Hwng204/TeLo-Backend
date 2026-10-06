using Domain.Entities.Examination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class ExamRoomConfiguration : IEntityTypeConfiguration<ExamRoom>
{
    public void Configure(EntityTypeBuilder<ExamRoom> builder)
    {
        builder.ToTable("exam_rooms", table => table.HasCheckConstraint("ck_exam_rooms_limit", "candidate_limit > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("bigint unsigned").ValueGeneratedOnAdd();
        builder.Property(x => x.ExamId).HasColumnName("exam_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.RoomId).HasColumnName("room_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.CandidateLimit).HasColumnName("candidate_limit").HasColumnType("int unsigned").IsRequired();
        builder.HasIndex(x => new { x.ExamId, x.RoomId }).IsUnique().HasDatabaseName("uq_exam_rooms_exam_room");
        builder.HasIndex(x => new { x.ExamId, x.Code }).IsUnique().HasDatabaseName("uq_exam_rooms_exam_code");
        builder.HasIndex(x => x.RoomId).HasDatabaseName("idx_exam_rooms_room");
        builder.HasOne(x => x.Exam).WithMany(x => x.Rooms)
            .HasForeignKey(x => x.ExamId).HasConstraintName("fk_exam_rooms_exam")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Room).WithMany()
            .HasForeignKey(x => x.RoomId).HasConstraintName("fk_exam_rooms_room")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
