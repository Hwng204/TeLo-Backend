using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Application.Validators;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

public sealed class StudentService(IStudentRepository studentRepository) : IStudentService
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
        ValidateId(query.AcademicYearId, "academicYearId", errors);
        ValidateId(query.SchoolBranchId, "schoolBranchId", errors);
        if (errors.Count > 0)
        {
            return ValidationFailure<DirectoryPage<StudentListItem>>(errors);
        }

        var result = await studentRepository.ListStudentsAsync(
            new StudentDirectoryFilter(
                scope, search, query.GradeLevelId, query.ClassId, query.SchoolBranchId,
                status, query.Page, query.PageSize, query.AcademicYearId),
            cancellationToken);
        if (result.Value is null)
        {
            return MapFailure<DirectoryPage<StudentListItem>>(
                result.Status, SchoolDirectoryErrorCodes.StudentNotFound);
        }

        var items = result.Value.Items.Select(s => new StudentListItem(
            s.Id, s.Code, s.FullName, s.DateOfBirth, s.Gender, s.GradeLevelId, s.GradeLevelName,
            s.ClassId, s.ClassName, s.SchoolBranchId, s.SchoolBranchName, s.Status)).ToList();
        return ServiceResult<DirectoryPage<StudentListItem>>.Success(
            new DirectoryPage<StudentListItem>(
                items, query.Page, query.PageSize, result.Value.TotalCount));
    }

    public async Task<ServiceResult<StudentDetailDto>> GetStudentAsync(
        DirectoryScope scope,
        ulong studentId,
        CancellationToken cancellationToken)
    {
        var result = await studentRepository.GetStudentAsync(scope, studentId, cancellationToken);
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
            student.Status, current, history));
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

        var result = await studentRepository.ListStudentScoresAsync(
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

    public async Task<ServiceResult<StudentDetailDto>> CreateStudentAsync(
        ulong schoolId,
        CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var errors = SchoolDirectoryValidator.ValidateCreateStudent(request, out var code, out var fullName, out var status);
        if (errors.Count > 0)
        {
            return Invalid<StudentDetailDto>(errors);
        }

        var created = await studentRepository.CreateStudentAsync(
            new CreateStudentCommand(
                schoolId, code, fullName, request.DateOfBirth, Trim(request.Gender),
                status, request.SchoolClassId),
            cancellationToken);
        return await StudentResultAsync(schoolId, created, cancellationToken);
    }

    public async Task<ServiceResult<string>> SuggestStudentCodeAsync(
        ulong schoolId,
        ulong classId,
        CancellationToken cancellationToken)
    {
        var code = await studentRepository.SuggestStudentCodeAsync(schoolId, classId, cancellationToken);
        return ServiceResult<string>.Success(code);
    }

    public async Task<ServiceResult<StudentDetailDto>> UpdateStudentAsync(
        ulong schoolId,
        ulong studentId,
        UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var errors = SchoolDirectoryValidator.ValidateUpdateStudent(request, out var code, out var fullName, out var status);

        if (errors.Count > 0)
        {
            return Invalid<StudentDetailDto>(errors);
        }

        var updated = await studentRepository.UpdateStudentAsync(
            new UpdateStudentCommand(
                schoolId, studentId, code, fullName, request.DateOfBirth, Trim(request.Gender),
                status, request.SchoolClassId),
            cancellationToken);
        return await StudentResultAsync(schoolId, updated, cancellationToken);
    }

    public async Task<ServiceResult<StudentDetailDto>> TransferStudentClassAsync(
        ulong schoolId,
        ulong studentId,
        TransferStudentClassRequest request,
        CancellationToken cancellationToken)
    {
        var errors = SchoolDirectoryValidator.ValidateTransferStudentClass(request);
        if (errors.Count > 0)
        {
            return Invalid<StudentDetailDto>(errors);
        }

        var transferred = await studentRepository.TransferStudentClassAsync(
            new TransferStudentClassCommand(
                schoolId, studentId, request.SchoolClassId, request.EffectiveOn),
            cancellationToken);
        return await StudentResultAsync(schoolId, transferred, cancellationToken);
    }

    public async Task<ServiceResult<StudentDetailDto>> DeleteStudentAsync(
        ulong schoolId,
        ulong studentId,
        CancellationToken cancellationToken)
    {
        var deleted = await studentRepository.DeactivateStudentAsync(
            schoolId, studentId, cancellationToken);
        return await StudentResultAsync(schoolId, deleted, cancellationToken);
    }

    // ─── PRIVATE HELPERS ────────────────────────────────────────────────────────

    private async Task<ServiceResult<StudentDetailDto>> StudentResultAsync(
        ulong schoolId,
        DirectoryWriteResult<ulong> write,
        CancellationToken cancellationToken) =>
        write.Status is DirectoryWriteStatus.Success
            ? await GetStudentAsync(
                DirectoryScope.ForSchool(schoolId), write.Value, cancellationToken)
            : MapWriteFailure<StudentDetailDto>(write.Status);

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
        DirectoryWriteStatus.StudentNotFound =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.StudentNotFound, "Không tìm thấy học sinh."),
        DirectoryWriteStatus.DuplicateStudentCode =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.StudentCodeDuplicate, "Mã học sinh đã tồn tại trong trường."),
        DirectoryWriteStatus.StudentNotEnrolledInYear =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Validation, "Học sinh chưa có thông tin học tập trong năm học hiện tại."),
        DirectoryWriteStatus.UseClassTransfer =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Validation, "Học sinh đã được xếp lớp. Vui lòng sử dụng chức năng chuyển lớp."),
        DirectoryWriteStatus.InvalidEffectiveDate =>
            ServiceResult<T>.Failure(SchoolDirectoryErrorCodes.Validation, "Ngày áp dụng chuyển lớp không hợp lệ."),
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

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
