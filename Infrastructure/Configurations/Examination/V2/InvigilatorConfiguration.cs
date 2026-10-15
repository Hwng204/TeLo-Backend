using Domain.Entities.Examination;
using Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Examination;

public sealed class InvigilatorConfiguration : IEntityTypeConfiguration<Invigilator>
{
    public void Configure(EntityTypeBuilder<Invigilator> builder)
    {
        builder.ToTable("invigilator");
        builder.HasKey(x => new { x.TeacherId, x.RoomId });
        builder.Property(x => x.TeacherId).HasColumnName("teacher_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.RoomId).HasColumnName("room_id").HasColumnType("bigint unsigned").IsRequired();
        builder.Property(x => x.Position).HasColumnName("position").HasMaxLength(50).IsRequired();
        builder.HasOne<Teacher>().WithMany()
            .HasForeignKey(x => x.TeacherId).HasConstraintName("fk_invigilator_teacher_id")
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Room>().WithMany()
            .HasForeignKey(x => x.RoomId).HasConstraintName("fk_invigilator_room_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
