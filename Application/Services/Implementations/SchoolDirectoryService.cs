using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

// Error codes shared by the read and the admin directory services; the controllers map them to
// status codes, so they must stay stable.
public static class SchoolDirectoryErrorCodes
{
    public const string Validation = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string SchoolScopeRequired = "SCHOOL_SCOPE_REQUIRED";
    public const string SchoolNotFound = "SCHOOL_NOT_FOUND";
    public const string StudentNotFound = "STUDENT_NOT_FOUND";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string ActiveAcademicYearNotFound = "ACTIVE_ACADEMIC_YEAR_NOT_FOUND";
    public const string StudentCodeDuplicate = "STUDENT_CODE_DUPLICATE";
    public const string ClassCodeDuplicate = "CLASS_CODE_DUPLICATE";
    public const string TeacherAlreadyHomeroom = "TEACHER_ALREADY_HOMEROOM";
    public const string ClassHasActiveStudents = "CLASS_HAS_ACTIVE_STUDENTS";
    public const string ImportBatchNotFound = "IMPORT_BATCH_NOT_FOUND";
    public const string ImportFileInvalid = "IMPORT_FILE_INVALID";
    public const string ImportBatchStateInvalid = "IMPORT_BATCH_STATE_INVALID";
    public const string ImportHasInvalidRows = "IMPORT_HAS_INVALID_ROWS";
}

public sealed class SchoolDirectoryService(ISchoolDirectoryRepository repository)
    : ISchoolDirectoryService
{
    internal const int MaxPageSize = 100;
    internal const int MaxSearchLength = 100;
    private const string ActiveYearStatus = "ACTIVE";

    public async Task<ServiceResult<DirectoryPage<StudentListItem>>> ListStudentsAsync(
        DirectoryScope scope,
        StudentListQuery query,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var search = NormalizeSearch(query.Search, errors);
        var status = NormalizeStatus(query.Status, StudentStatusCodes.All, errors);
        ValidatePaging(query.Page, query.PageSize, errors);
        ValidateId(query.GradeLevelId, "gradeLevelId", errors);
        ValidateId(query.ClassId, "classId", errors);
        ValidateId(query.SchoolBranchId, "schoolBranchId", errors);
        if (errors.Count > 0)
        {
            return ValidationFailure<DirectoryPage<StudentListItem>>(errors);
        }

        var result = await repository.ListStudentsAsync(
            new StudentDirectoryFilter(
                scope, search, query.GradeLevelId, query.ClassId, query.SchoolBranchId,
                status, query.Page, query.PageSize),
            cancellationToken);
        if (result.Value is null)
        {
            return MapFailure<DirectoryPage<StudentListItem>>(
                result.Status, SchoolDirectoryErrorCodes.StudentNotFound);
        }

        var items = result.Value.Items.Select(s => new StudentListItem(
            s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender, s.GradeLevelId, s.GradeLevelName,
            s.ClassId, s.ClassName, s.SchoolBranchId, s.SchoolBranchName, s.Status,
            s.AdmissionDate)).ToList();
        return ServiceResult<DirectoryPage<StudentListItem>>.Success(
            new DirectoryPage<StudentListItem>(
                items, query.Page, query.PageSize, result.Value.TotalCount));
    }

    public async Task<ServiceResult<StudentDetailDto>> GetStudentAsync(
        DirectoryScope scope,
        ulong studentId,
        CancellationToken cancellationToken)
    {
        var result = await repository.GetStudentAsync(scope, studentId, cancellationToken);
        if (result.Value is not { } student)
        {
            return MapFailure<StudentDetailDto>(
                result.Status, SchoolDirectoryErrorCodes.StudentNotFound);
        }

        var history = student.History.Select(h => new StudentAcademicHistoryItem(
            h.AcademicYearId, h.AcademicYearName, h.GradeLevelId, h.GradeLevelName,
            h.ClassId, h.ClassName, h.HomeroomTeacherId, h.HomeroomTeacherName,
            h.EnrollmentStatus)).ToList();

        // History is newest-first. The live enrollment of the active year wins; a year may hold
        // several rows after a class transfer, so prefer the ACTIVE one, then any row of that year,
        // otherwise the most recent class.
        var currentRow =
            student.History.FirstOrDefault(h =>
                h.AcademicYearStatus == ActiveYearStatus &&
                h.EnrollmentStatus == StudentEnrollmentStatusCodes.Active)
            ?? student.History.FirstOrDefault(h => h.AcademicYearStatus == ActiveYearStatus)
            ?? student.History.FirstOrDefault();
        var current = currentRow is null
            ? null
            : new StudentCurrentClassDto(
                currentRow.AcademicYearId, currentRow.AcademicYearName,
                currentRow.GradeLevelId, currentRow.GradeLevelName,
                currentRow.ClassId, currentRow.ClassName,
                currentRow.SchoolBranchId, currentRow.SchoolBranchName,
                currentRow.HomeroomTeacherId, currentRow.HomeroomTeacherName);

        return ServiceResult<StudentDetailDto>.Success(new StudentDetailDto(
            student.Id, student.Code, student.FullName, student.DateOfBirth, student.Gender,
            student.AdmissionDate, student.Status, current, history));
    }

    public async Task<ServiceResult<DirectoryPage<StudentScoreItem>>> GetStudentScoresAsync(
        DirectoryScope scope,
        ulong studentId,
        ulong classId,
        PageQuery page,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidatePaging(page.Page, page.PageSize, errors);
        if (classId == 0)
        {
            errors["classId"] = ["Lớp học không hợp lệ."];
        }

        if (errors.Count > 0)
        {
            return ValidationFailure<DirectoryPage<StudentScoreItem>>(errors);
        }

        var result = await repository.ListStudentScoresAsync(
            scope, studentId, classId, page.Page, page.PageSize, cancellationToken);
        if (result.Value is null)
        {
            return MapFailure<DirectoryPage<StudentScoreItem>>(
                result.Status, SchoolDirectoryErrorCodes.StudentNotFound);
        }

        var items = result.Value.Items.Select(s => new StudentScoreItem(
            s.AttemptId, s.ExamId, s.ExamName, s.SemesterId, s.SemesterName,
            s.SubjectId, s.SubjectName, s.ExamDate, s.TotalScore, s.ResultPublishedAt)).ToList();
        return ServiceResult<DirectoryPage<StudentScoreItem>>.Success(
            new DirectoryPage<StudentScoreItem>(
                items, page.Page, page.PageSize, result.Value.TotalCount));
    }

    public async Task<ServiceResult<DirectoryPage<ClassListItem>>> ListClassesAsync(
        DirectoryScope scope,
        ClassListQuery query,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var search = NormalizeSearch(query.Search, errors);
        var status = NormalizeStatus(query.Status, SchoolClassStatusCodes.All, errors);
        ValidatePaging(query.Page, query.PageSize, errors);
        ValidateId(query.GradeLevelId, "gradeLevelId", errors);
        ValidateId(query.AcademicYearId, "academicYearId", errors);
        ValidateId(query.SchoolBranchId, "schoolBranchId", errors);
        if (errors.Count > 0)
        {
            return ValidationFailure<DirectoryPage<ClassListItem>>(errors);
        }

        var result = await repository.ListClassesAsync(
            new ClassDirectoryFilter(
                scope, search, query.GradeLevelId, query.AcademicYearId,
                query.SchoolBranchId, status, query.Page, query.PageSize),
            cancellationToken);
        if (result.Value is null)
        {
            return MapFailure<DirectoryPage<ClassListItem>>(
                result.Status, SchoolDirectoryErrorCodes.ClassNotFound);
        }

        return ServiceResult<DirectoryPage<ClassListItem>>.Success(
            new DirectoryPage<ClassListItem>(
                result.Value.Items.Select(ToItem).ToList(),
                query.Page, query.PageSize, result.Value.TotalCount));
    }

    public async Task<ServiceResult<ClassDetailDto>> GetClassAsync(
        DirectoryScope scope,
        ulong classId,
        PageQuery rosterPage,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidatePaging(rosterPage.Page, rosterPage.PageSize, errors);
        if (errors.Count > 0)
        {
            return ValidationFailure<ClassDetailDto>(errors);
        }

        var result = await repository.GetClassAsync(
            scope, classId, rosterPage.Page, rosterPage.PageSize, cancellationToken);
        if (result.Value is not { } detail)
        {
            return MapFailure<ClassDetailDto>(
                result.Status, SchoolDirectoryErrorCodes.ClassNotFound);
        }

        var students = detail.Students.Items.Select(s => new ClassStudentItem(
            s.StudentId, s.StudentCode, s.FullName, s.DateOfBirth, s.Gender,
            s.ClassName, s.StudentStatus)).ToList();
        return ServiceResult<ClassDetailDto>.Success(new ClassDetailDto(
            ToItem(detail.Class),
            new DirectoryPage<ClassStudentItem>(
                students, rosterPage.Page, rosterPage.PageSize, detail.Students.TotalCount)));
    }

    public async Task<ServiceResult<SchoolDirectoryReferenceData>> GetReferenceDataAsync(
        DirectoryScope scope,
        ulong? academicYearId,
        CancellationToken cancellationToken)
    {
        if (academicYearId == 0)
        {
            return ValidationFailure<SchoolDirectoryReferenceData>(new Dictionary<string, string[]>
            {
                ["academicYearId"] = ["Năm học không hợp lệ."]
            });
        }

        var result = await repository.GetReferenceDataAsync(
            scope, academicYearId, cancellationToken);
        if (result.Value is not { } rows)
        {
            return MapFailure<SchoolDirectoryReferenceData>(
                result.Status, SchoolDirectoryErrorCodes.ClassNotFound);
        }

        static IReadOnlyList<DirectoryOption> Options(IEnumerable<DirectoryOptionRow> source) =>
            source.Select(o => new DirectoryOption(o.Id, o.Code, o.Name)).ToList();

        return ServiceResult<SchoolDirectoryReferenceData>.Success(new SchoolDirectoryReferenceData(
            Options(rows.SchoolBranches), Options(rows.GradeLevels),
            Options(rows.AcademicYears), Options(rows.Classes),
            StudentStatusCodes.All, SchoolClassStatusCodes.All));
    }

    private static ClassListItem ToItem(ClassDirectoryRow c) => new(
        c.Id, c.Code, c.Name, c.GradeLevelId, c.GradeLevelName, c.AcademicYearId,
        c.AcademicYearName, c.SchoolBranchId, c.SchoolBranchName, c.HomeroomTeacherId,
        c.HomeroomTeacherName, c.StudentCount, c.Status);

    private static string? NormalizeSearch(string? search, Dictionary<string, string[]> errors)
    {
        var trimmed = search?.Trim();
        if (trimmed is { Length: > MaxSearchLength })
        {
            errors["search"] = [$"Từ khóa tìm kiếm tối đa {MaxSearchLength} ký tự."];
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string? NormalizeStatus(
        string? status,
        IReadOnlyCollection<string> allowed,
        Dictionary<string, string[]> errors)
    {
        var normalized = status?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (!allowed.Contains(normalized))
        {
            errors["status"] = [$"Trạng thái phải là một trong: {string.Join(", ", allowed)}."];
        }

        return normalized;
    }

    private static void ValidatePaging(int page, int pageSize, Dictionary<string, string[]> errors)
    {
        if (page < 1)
        {
            errors["page"] = ["Trang phải lớn hơn hoặc bằng 1."];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"Kích thước trang phải từ 1 đến {MaxPageSize}."];
        }
    }

    private static void ValidateId(ulong? id, string field, Dictionary<string, string[]> errors)
    {
        if (id == 0)
        {
            errors[field] = ["Giá trị không hợp lệ."];
        }
    }

    private static ServiceResult<T> ValidationFailure<T>(Dictionary<string, string[]> errors) =>
        ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.Validation, "Tham số truy vấn không hợp lệ.", errors);

    private static ServiceResult<T> MapFailure<T>(
        DirectoryReadStatus status,
        string notFoundCode) => status switch
    {
        DirectoryReadStatus.ActorNotFound =>
            ServiceResult<T>.Failure(
                SchoolDirectoryErrorCodes.Unauthorized, "Tài khoản không hợp lệ."),
        DirectoryReadStatus.SchoolScopeMissing =>
            ServiceResult<T>.Failure(
                SchoolDirectoryErrorCodes.SchoolScopeRequired,
                "Tài khoản chưa được gán cơ sở/trường."),
        DirectoryReadStatus.SchoolNotFound =>
            ServiceResult<T>.Failure(
                SchoolDirectoryErrorCodes.SchoolNotFound, "Không tìm thấy trường."),
        DirectoryReadStatus.ActiveAcademicYearNotFound =>
            ServiceResult<T>.Failure(
                SchoolDirectoryErrorCodes.ActiveAcademicYearNotFound,
                "Trường chưa có năm học đang áp dụng. Vui lòng chọn năm học."),
        _ => ServiceResult<T>.Failure(
            notFoundCode,
            notFoundCode == SchoolDirectoryErrorCodes.StudentNotFound
                ? "Không tìm thấy học sinh."
                : "Không tìm thấy lớp học.")
    };
}
