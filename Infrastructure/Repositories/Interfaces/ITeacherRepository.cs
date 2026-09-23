using Domain.Entities.Identity;

namespace Infrastructure.Repositories.Interface;

// Explicit school/branch means an operational admin route. Otherwise resolve from the actor in DB.
public sealed record TeacherScope(ulong ActorUserId, ulong? SchoolId = null, ulong? BranchId = null);
public sealed record TeacherResolvedScope(ulong SchoolId, ulong? BranchId, bool IsAdmin, bool IsUnitActive);
public sealed record TeacherListRow(
    ulong Id, string? StaffCode, string FullName, string? Department,
    ulong? MainSubjectId, string? MainSubjectName, bool? Gender,
    DateOnly? JoinedOn, string EmploymentStatus, string AccountStatus,
    ulong SchoolId, ulong SchoolBranchId, string SchoolBranchName, uint Version);
public sealed record TeacherFilter(
    string? Search, string? Department, ulong? MainSubjectId, string? EmploymentStatus,
    string? AccountStatus, bool? Gender, ulong? BranchId, string SortBy,
    bool Descending, int Page, int PageSize);
public sealed record TeacherOptionRow(ulong Id, string Code, string Name, string Status);
public sealed record TeacherReferenceRows(
    IReadOnlyList<TeacherOptionRow> Subjects, IReadOnlyList<string> Departments,
    IReadOnlyList<TeacherOptionRow> Branches);

public interface ITeacherRepository
{
    Task<TeacherResolvedScope> ResolveScopeAsync(TeacherScope scope, bool write, CancellationToken ct);
    Task<(IReadOnlyList<TeacherListRow> Items, int Total)> ListAsync(TeacherResolvedScope scope, TeacherFilter filter, CancellationToken ct);
    Task<Teacher> GetAsync(TeacherResolvedScope scope, ulong id, bool tracking, CancellationToken ct);
    Task<TeacherReferenceRows> ReferencesAsync(TeacherResolvedScope scope, CancellationToken ct);
    Task<(IReadOnlyList<TeacherOptionRow> Items, int Total)> SchoolsAsync(ulong actorId, string? search, int page, int size, CancellationToken ct);
    Task<(IReadOnlyList<TeacherOptionRow> Items, int Total)> BranchesAsync(ulong actorId, ulong schoolId, string? search, int page, int size, CancellationToken ct);
    Task ValidateReferencesAsync(TeacherProfileValues profile, ulong? existingSubjectId, CancellationToken ct);
    Task ValidateUniqueAsync(ulong? teacherId, ulong? userId, string? staffCode, string username, string email, CancellationToken ct);
    Task<Role> GetTeacherRoleAsync(CancellationToken ct);
    void EnsureManageable(Teacher teacher);
    void Add(Teacher teacher);
    Task SaveAsync(CancellationToken ct);
}

public sealed record TeacherProfileValues(
    string StaffCode, string FullName, string? Department, ulong? MainSubjectId,
    string? Specialization, string? Position, bool? Gender, string? Phone,
    string? WorkEmail, DateOnly? DateOfBirth, DateOnly? JoinedOn, string EmploymentStatus);
