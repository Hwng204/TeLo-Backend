namespace Application.DTOs;

public sealed record TeacherListQuery(
    string? Search = null,
    string? Department = null,
    ulong? MainSubjectId = null,
    string? EmploymentStatus = null,
    string? AccountStatus = null,
    bool? Gender = null,
    ulong? SchoolBranchId = null,
    string SortBy = "fullName",
    string SortDirection = "asc",
    int Page = 1,
    int PageSize = 20);

public sealed record TeacherPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record TeacherOption(ulong Id, string Code, string Name, string Status);
public sealed record TeacherSelectionQuery(string? Search = null, int Page = 1, int PageSize = 20);

public sealed record TeacherProfileRequest(
    string StaffCode,
    string FullName,
    string? Department = null,
    ulong? MainSubjectId = null,
    string? Specialization = null,
    string? Position = null,
    bool? Gender = null,
    string? Phone = null,
    string? WorkEmail = null,
    DateOnly? DateOfBirth = null,
    DateOnly? JoinedOn = null,
    string EmploymentStatus = "WORKING");

public sealed record CreateTeacherRequest(
    TeacherProfileRequest Profile, string Username, string Email, string Password);
public sealed record UpdateTeacherRequest(TeacherProfileRequest Profile, uint Version);
public sealed record UpdateTeacherAccountRequest(string Username, string Email, string Status, uint Version);
public sealed record ResetTeacherPasswordRequest(string NewPassword, uint Version);

public sealed record TeacherListItem(
    ulong Id, string? StaffCode, string FullName, string? Department,
    ulong? MainSubjectId, string? MainSubjectName, bool? Gender,
    DateOnly? JoinedOn, string EmploymentStatus, string AccountStatus,
    ulong SchoolId, ulong SchoolBranchId, string SchoolBranchName, uint Version);

public sealed record TeacherDetailDto(
    TeacherListItem Teacher, string? Specialization, string? Position,
    string? Phone, string? WorkEmail, DateOnly? DateOfBirth,
    ulong? HomeroomClassId, string? HomeroomClassName, IReadOnlyList<string> Roles);

public sealed record TeacherAccountDto(
    ulong TeacherId, ulong UserId, string Username, string Email, string Status,
    IReadOnlyList<string> Roles, DateTime CreatedAt, uint Version);

public sealed record TeacherReferenceData(
    IReadOnlyList<TeacherOption> Subjects,
    IReadOnlyList<string> Departments,
    IReadOnlyList<TeacherOption> Branches,
    IReadOnlyList<string> EmploymentStatuses,
    IReadOnlyList<string> AccountStatuses);
