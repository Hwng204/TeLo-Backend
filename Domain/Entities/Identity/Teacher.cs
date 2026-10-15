namespace Domain.Entities.Identity;

// Staff code lives in User.MoetIdentifier and employment status in User.Status (DB v2).
public sealed class Teacher
{
    public ulong Id { get; set; }
    public ulong UserId { get; set; }
    public string? Specialization { get; set; }
    public bool? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    public User User { get; set; } = null!;
    public ICollection<TeacherAssignment> Assignments { get; set; } = new List<TeacherAssignment>();
}
