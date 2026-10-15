using Domain.Entities.Examination;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class RoomStudentConfiguration : IEntityTypeConfiguration<RoomStudent>
{
    public void Configure(EntityTypeBuilder<RoomStudent> builder)
    {
        builder.ToTable("room_student");
        builder.HasKey(x => new { x.RoomId, x.StudentId });
        builder.Property(x => x.RoomId).HasColumnName("room_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.StudentId).HasColumnName("student_id").HasColumnType("bigint unsigned").IsRequired();
        builder.HasOne<Room>().WithMany()
            .HasForeignKey(x => x.RoomId).HasConstraintName("fk_room_student_room_id")
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Student>().WithMany()
            .HasForeignKey(x => x.StudentId).HasConstraintName("fk_room_student_student_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
