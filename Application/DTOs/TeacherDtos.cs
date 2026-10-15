namespace Application.DTOs;

// DB v2: department, mainSubjectId, position, joinedOn and version are accepted and ignored on requests
// (removed from these records, so JSON/query values for them are dropped) and no longer returned.
public sealed record TeacherListQuery(
    string? Search = null,
    string? EmploymentStatus = null,
    string? AccountStatus = null,
    bool? Gender = null,
    ulong? SchoolBranchId = null,
    string SortBy = "fullName",
    string SortDirection = "asc",
    int Page = 1,
    int PageSize = 20,
    ulong? AcademicYearId = null);

public sealed record TeacherPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record TeacherOption(ulong Id, string Code, string Name, string Status);
public sealed record TeacherSelectionQuery(string? Search = null, int Page = 1, int PageSize = 20);

public sealed record TeacherProfileRequest(
    string StaffCode,
    string FullName,
    string? Specialization = null,
    bool? Gender = null,
    string? Phone = null,
    string? WorkEmail = null,
    DateOnly? DateOfBirth = null,
    string EmploymentStatus = "WORKING");

public sealed record CreateTeacherRequest(
    TeacherProfileRequest Profile, string Username, string Email, string Password);
public sealed record UpdateTeacherRequest(TeacherProfileRequest Profile);
public sealed record UpdateTeacherAccountRequest(string Username, string Email, string Status);
public sealed record ResetTeacherPasswordRequest(string NewPassword);

// StaffCode ← user.moet_identifier; EmploymentStatus ← WORKING when user.status is ACTIVE, else INACTIVE.
public sealed record TeacherListItem(
    ulong Id, string? StaffCode, string FullName, bool? Gender,
    string EmploymentStatus, string AccountStatus,
    ulong SchoolId, ulong SchoolBranchId, string SchoolBranchName);

public sealed record TeacherDetailDto(
    TeacherListItem Teacher, string? Specialization,
    string? Phone, string? WorkEmail, DateOnly? DateOfBirth,
    ulong? HomeroomClassId, string? HomeroomClassName, IReadOnlyList<string> Roles);

// Email ← teacher.email (the same column as the profile's workEmail).
public sealed record TeacherAccountDto(
    ulong TeacherId, ulong UserId, string Username, string Email, string Status,
    IReadOnlyList<string> Roles, DateTime CreatedAt);

public sealed record TeacherReferenceData(
    IReadOnlyList<TeacherOption> Subjects,
    IReadOnlyList<string> Departments,
    IReadOnlyList<TeacherOption> Branches,
    IReadOnlyList<string> EmploymentStatuses,
    IReadOnlyList<string> AccountStatuses);
