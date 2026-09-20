namespace Infrastructure.Repositories.Interface;

public enum DirectoryWriteStatus
{
    Success,
    SchoolNotFound,
    StudentNotFound,
    ClassNotFound,
    BranchNotInSchool,
    AcademicYearInvalid,
    GradeLevelNotFound,
    DuplicateStudentCode,
    DuplicateClassCode,
    TeacherNotInSchool,
    TeacherAlreadyHomeroom,
    ClassHasActiveStudents
}

public sealed record DirectoryWriteResult<T>(DirectoryWriteStatus Status, T? Value)
{
    public static DirectoryWriteResult<T> Ok(T value) => new(DirectoryWriteStatus.Success, value);

    public static DirectoryWriteResult<T> Fail(DirectoryWriteStatus status) => new(status, default);
}

public sealed record CreateStudentCommand(
    ulong SchoolId,
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    DateOnly AdmissionDate,
    string Status,
    ulong SchoolClassId);

public sealed record UpdateStudentCommand(
    ulong SchoolId,
    ulong StudentId,
    string Code,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    DateOnly AdmissionDate,
    string Status,
    ulong? SchoolClassId);

public sealed record CreateClassCommand(
    ulong SchoolId,
    ulong SchoolBranchId,
    string Code,
    string Name,
    ulong AcademicYearId,
    ulong GradeLevelId,
    string Status,
    ulong? HomeroomTeacherId);

public sealed record UpdateClassCommand(
    ulong SchoolId,
    ulong ClassId,
    ulong SchoolBranchId,
    string Code,
    string Name,
    ulong AcademicYearId,
    ulong GradeLevelId,
    string Status,
    ulong? HomeroomTeacherId);

// Write side of the directory, only reachable from admin routes that name the school explicitly.
// Every command re-validates that each referenced row belongs to that school.
public interface ISchoolDirectoryAdminRepository
{
    Task<DirectoryWriteResult<ulong>> CreateStudentAsync(
        CreateStudentCommand command,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> UpdateStudentAsync(
        UpdateStudentCommand command,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> DeactivateStudentAsync(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> CreateClassAsync(
        CreateClassCommand command,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> UpdateClassAsync(
        UpdateClassCommand command,
        CancellationToken cancellationToken);

    Task<DirectoryWriteResult<ulong>> DeactivateClassAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken);
}
