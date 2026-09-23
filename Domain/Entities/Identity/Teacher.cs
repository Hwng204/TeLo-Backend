using Domain.Entities.Organization;
using Domain.Entities.Academic;

namespace Domain.Entities.Identity;

public sealed class Teacher
{
    public ulong Id { get; set; }
    public ulong UserId { get; set; }
    public string? StaffCode { get; set; }
    public string? Department { get; set; }
    public ulong? MainSubjectId { get; set; }
    public DateOnly? JoinedOn { get; set; }
    public string EmploymentStatus { get; set; } = "WORKING";
    public uint Version { get; set; } = 1;
    public string? Specialization { get; set; }
    public string? Position { get; set; }
    public bool? Gender { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public ulong? ClassId { get; set; }

    public User User { get; set; } = null!;
    public Subject? MainSubject { get; set; }
    public SchoolClass? SchoolClass { get; set; }
}
