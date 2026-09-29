namespace Application.DTOs;

// No DataAnnotations on purpose: the admin service validates every field so that field problems
// come back as 422 VALIDATION_ERROR. A 400 here means the JSON itself could not be bound.
// Text and date fields are nullable for the same reason: a non-nullable string gets an implicit
// [Required] from MVC, which answered a missing code with a 400 before the service could run.
public sealed record CreateStudentRequest(
    string? Code,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    DateOnly? AdmissionDate,
    string? Status,
    ulong SchoolClassId);

public sealed record UpdateStudentRequest(
    string? Code,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    DateOnly? AdmissionDate,
    string? Status,
    ulong? SchoolClassId);

public sealed record CreateClassRequest(
    ulong SchoolBranchId,
    string? Code,
    string? Name,
    ulong AcademicYearId,
    ulong GradeLevelId,
    string? Status,
    ulong? HomeroomTeacherId);

public sealed record UpdateClassRequest(
    ulong SchoolBranchId,
    string? Code,
    string? Name,
    ulong AcademicYearId,
    ulong GradeLevelId,
    string? Status,
    ulong? HomeroomTeacherId);

// Moves a student to another class of the same academic year while keeping the earlier class in
// the history. EffectiveOn defaults to today.
public sealed record TransferStudentClassRequest(
    ulong SchoolClassId,
    DateOnly? EffectiveOn);
