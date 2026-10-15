using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Application.Validators;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

public sealed class ClassService(
    IClassRepository classRepository,
    ISchoolRepository schoolRepository) : IClassService
{
    internal const int MaxPageSize = 100;
    internal const int MaxSearchLength = 100;
    private const int RosterPageSize = 20;

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

        var result = await classRepository.ListClassesAsync(
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

        var result = await classRepository.GetClassAsync(
            scope, classId, rosterPage.Page, rosterPage.PageSize, cancellationToken);
        if (result.Value is not { } detail)
        {
            return MapFailure<ClassDetailDto>(
                result.Status, SchoolDirectoryErrorCodes.ClassNotFound);
        }

        var students = detail.Students.Items.Select(s => new ClassStudentItem(
            s.StudentId, s.StudentCode, s.FullName, s.DateOfBirth, s.Gender,
            s.ClassName, s.StudentStatus, s.EnrollmentStatus)).ToList();
        return ServiceResult<ClassDetailDto>.Success(new ClassDetailDto(
            ToItem(detail.Class),
            new DirectoryPage<ClassStudentItem>(
                students, rosterPage.Page, rosterPage.PageSize, detail.Students.TotalCount)));
    }

    public async Task<ServiceResult<ClassDetailDto>> CreateClassAsync(
        ulong schoolId,
        CreateClassRequest request,
        CancellationToken cancellationToken)
    {
        var errors = SchoolDirectoryValidator.ValidateCreateClass(request, out var name, out var code, out var status);
        if (errors.Count > 0)
        {
            return Invalid<ClassDetailDto>(errors);
        }

        var created = await classRepository.CreateClassAsync(
            new CreateClassCommand(
                schoolId, request.SchoolBranchId, code, name, request.AcademicYearId,
                request.GradeLevelId, status, NullableId(request.HomeroomTeacherId)),
            cancellationToken);
        return await ClassResultAsync(schoolId, created, cancellationToken);
    }

    public async Task<ServiceResult<ClassDetailDto>> UpdateClassAsync(
        ulong schoolId,
        ulong classId,
        UpdateClassRequest request,
        CancellationToken cancellationToken)
    {
        var errors = SchoolDirectoryValidator.ValidateUpdateClass(request, out var name, out var code, out var status);
        if (errors.Count > 0)
        {
            return Invalid<ClassDetailDto>(errors);
        }

        var updated = await classRepository.UpdateClassAsync(
            new UpdateClassCommand(
                schoolId, classId, request.SchoolBranchId, code, name, request.AcademicYearId,
                request.GradeLevelId, status, NullableId(request.HomeroomTeacherId)),
            cancellationToken);
        return await ClassResultAsync(schoolId, updated, cancellationToken);
    }

    public async Task<ServiceResult<ClassDetailDto>> DeleteClassAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken)
    {
        var deleted = await classRepository.DeactivateClassAsync(schoolId, classId, cancellationToken);
        return await ClassResultAsync(schoolId, deleted, cancellationToken);
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

        var result = await schoolRepository.GetReferenceDataAsync(
            scope, academicYearId, cancellationToken);
        if (result.Value is not { } rows)
        {
            return MapFailure<SchoolDirectoryReferenceData>(
                result.Status, SchoolDirectoryErrorCodes.ClassNotFound);
        }

        static IReadOnlyList<DirectoryOption> Options(IEnumerable<DirectoryOptionRow> source) =>
            source.Select(o => new DirectoryOption(o.Id, o.Code, o.Name)).ToList();

        return ServiceResult<SchoolDirectoryReferenceData>.Success(new SchoolDirectoryReferenceData(
            rows.SchoolBranches.Select(b => new DirectoryBranchOption(
                b.Id, b.Code, b.Name, b.Status, b.ClassCount, b.StudentCount)).ToList(),
            Options(rows.GradeLevels),
            Options(rows.AcademicYears), Options(rows.Classes),
            StudentStatusCodes.All, SchoolClassStatusCodes.All));
    }

    // ─── PRIVATE HELPERS ────────────────────────────────────────────────────────

    private async Task<ServiceResult<ClassDetailDto>> ClassResultAsync(
        ulong schoolId,
        DirectoryWriteResult<ulong> write,
        CancellationToken cancellationToken) =>
        write.Status is DirectoryWriteStatus.Success
            ? await GetClassAsync(
                DirectoryScope.ForSchool(schoolId), write.Value,
                new PageQuery(1, RosterPageSize), cancellationToken)
            : MapWriteFailure<ClassDetailDto>(write.Status);

    private static ClassListItem ToItem(ClassDirectoryRow c) => new(
        c.Id, c.Code, c.Name, c.GradeLevelId, c.GradeLevelName, c.AcademicYearId,
        c.AcademicYearName, c.SchoolBranchId, c.SchoolBranchName, c.HomeroomTeacherId,
        c.HomeroomTeacherName, c.StudentCount, c.Status);

    private static ServiceResult<T> Invalid<T>(Dictionary<string, string[]> errors) =>
        ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Validation, "Dữ liệu không hợp lệ.", errors);

    private static ServiceResult<T> MapWriteFailure<T>(DirectoryWriteStatus status) => status switch
    {
        DirectoryWriteStatus.SchoolNotFound =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.SchoolNotFound, "Không tìm thấy trường."),
        DirectoryWriteStatus.BranchNotInSchool =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.SchoolNotFound, "Cơ sở không thuộc trường."),
        DirectoryWriteStatus.GradeLevelNotFound =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Validation, "Khối lớp không hợp lệ."),
        DirectoryWriteStatus.AcademicYearInvalid =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.ActiveAcademicYearNotFound, "Năm học không hợp lệ."),
        DirectoryWriteStatus.ClassNotFound =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.ClassNotFound, "Không tìm thấy lớp học."),
        DirectoryWriteStatus.TeacherNotInSchool =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Validation, "Giáo viên không thuộc trường."),
        DirectoryWriteStatus.TeacherAlreadyHomeroom =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.TeacherAlreadyHomeroom, "Giáo viên đã là chủ nhiệm lớp khác trong năm học này."),
        DirectoryWriteStatus.DuplicateClassCode =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.ClassCodeDuplicate, "Mã lớp đã tồn tại trong năm học của cơ sở."),
        DirectoryWriteStatus.DuplicateClassName =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.ClassCodeDuplicate, "Tên lớp đã tồn tại trong năm học của cơ sở."),
        DirectoryWriteStatus.ClassHasActiveStudents =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.ClassHasActiveStudents, "Không thể ngừng hoạt động lớp đang có học sinh theo học."),
        _ => ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Validation, "Thao tác không thành công.")
    };

    private static ServiceResult<T> MapFailure<T>(DirectoryReadStatus status, string notFoundCode) => status switch
    {
        DirectoryReadStatus.ActorNotFound =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Unauthorized, "Không xác định được danh tính người dùng."),
        DirectoryReadStatus.SchoolScopeMissing =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.SchoolScopeRequired, "Tài khoản chưa được gán vào trường/phân hiệu nào."),
        DirectoryReadStatus.SchoolNotFound =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.SchoolNotFound, "Không tìm thấy trường."),
        DirectoryReadStatus.ActiveAcademicYearNotFound =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.ActiveAcademicYearNotFound, "Không tìm thấy năm học đang hoạt động."),
        _ => ServiceResult<T>.Failure(notFoundCode, "Không tìm thấy dữ liệu.")
    };

    private static ServiceResult<T> ValidationFailure<T>(Dictionary<string, string[]> errors) =>
        ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Validation, "Dữ liệu không hợp lệ.", errors);

    private static string? NormalizeSearch(string? search, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        var trimmed = search.Trim();
        if (trimmed.Length > MaxSearchLength)
        {
            errors["search"] = [$"Từ khoá tối đa {MaxSearchLength} ký tự."];
        }
        return trimmed;
    }

    private static string? NormalizeStatus(string? status, IReadOnlyList<string> allowed, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var trimmed = status.Trim().ToUpperInvariant();
        if (!allowed.Contains(trimmed))
        {
            errors["status"] = ["Trạng thái không hợp lệ."];
        }
        return trimmed;
    }

    private static void ValidatePaging(int page, int pageSize, Dictionary<string, string[]> errors)
    {
        if (page < 1) errors["page"] = ["Trang phải lớn hơn hoặc bằng 1."];
        if (pageSize is < 1 or > MaxPageSize) errors["pageSize"] = [$"Số bản ghi mỗi trang từ 1 đến {MaxPageSize}."];
    }

    private static void ValidateId(ulong? id, string fieldName, Dictionary<string, string[]> errors)
    {
        if (id is 0) errors[fieldName] = ["Giá trị không hợp lệ."];
    }

    private static ulong? NullableId(ulong? id) => id is 0 ? null : id;
}
