namespace Application.DTOs;

public sealed record StudentImportColumnDto(
    string Key,
    string Header,
    bool Required,
    string Format,
    string Example);

// What the "view template" button shows: the columns, their formats, and the class codes that are
// valid for the chosen academic year. The .xlsx download carries the same information.
public sealed record StudentImportTemplateInfo(
    ulong AcademicYearId,
    string AcademicYearName,
    IReadOnlyList<StudentImportColumnDto> Columns,
    IReadOnlyList<DirectoryOption> Classes,
    IReadOnlyList<string> Genders,
    string DateFormat,
    int MaxRows,
    int MaxFileBytes);

public sealed record StudentImportFile(string FileName, byte[] Content);

public sealed record StudentImportRowErrorDto(string Field, string Message);

public sealed record StudentImportRowDto(
    int RowNumber,
    string Code,
    string FullName,
    string DateOfBirth,
    string Gender,
    string AdmissionDate,
    string ClassCode,
    ulong? ClassId,
    string? ClassName,
    bool IsValid,
    IReadOnlyList<StudentImportRowErrorDto> Errors,
    ulong? CreatedStudentId);

public sealed record StudentImportBatchDto(
    ulong Id,
    ulong SchoolId,
    string SchoolName,
    ulong AcademicYearId,
    string AcademicYearName,
    string Source,
    string Status,
    string FileName,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    string CreatedByName,
    DateTime CreatedAt,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewComment,
    DateTime? AppliedAt);

public sealed record StudentImportBatchDetailDto(
    StudentImportBatchDto Batch,
    DirectoryPage<StudentImportRowDto> Rows);

public sealed record StudentImportListQuery(
    string? Status,
    int Page = 1,
    int PageSize = 20);

public sealed record StudentImportRowsQuery(
    int Page = 1,
    int PageSize = 100,
    bool OnlyInvalid = false);

public sealed record RejectStudentImportRequest(string? Comment);
