using Domain.Entities.Organization;

namespace Domain.Entities.Identity;

public sealed class User
{
    public ulong Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ulong? SchoolBranchId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? MoetIdentifier { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public uint SecurityVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; }

    public SchoolBranch? SchoolBranch { get; set; }
    public ICollection<Student> Students { get; set; } = new List<Student>();
    public ICollection<Teacher> Teachers { get; set; } = new List<Teacher>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
