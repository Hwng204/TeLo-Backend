namespace Domain.Entities.Identity;

public sealed class Student
{
    public ulong Id { get; set; }
    public ulong? UserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public DateOnly AdmissionDate { get; set; }
    public string Status { get; set; } = StudentStatusCodes.Active;

    public User? User { get; set; }
    public ICollection<StudentEnrollment> Enrollments { get; set; } = new List<StudentEnrollment>();
}
