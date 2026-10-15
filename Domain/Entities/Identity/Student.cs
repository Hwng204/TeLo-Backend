namespace Domain.Entities.Identity;

// Code, full name and status live on the student's own User (username, full_name, status).
public sealed class Student
{
    public ulong Id { get; set; }
    public ulong UserId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }

    public User User { get; set; } = null!;
    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
}
