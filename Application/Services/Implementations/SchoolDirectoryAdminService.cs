using Application.Common;
using Application.DTOs;
using Application.Services.Interface;
using Domain.Entities.Identity;
using Domain.Entities.Organization;
using Infrastructure.Repositories.Interface;

namespace Application.Services.Implement;

public sealed class SchoolDirectoryAdminService(
    ISchoolDirectoryAdminRepository repository,
    ISchoolDirectoryService readService) : ISchoolDirectoryAdminService
{
    private const int RosterPageSize = 20;

    public async Task<ServiceResult<StudentDetailDto>> CreateStudentAsync(
        ulong schoolId,
        CreateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var code = Required(request.Code, "code", 64, errors);
        var fullName = Required(request.FullName, "fullName", 255, errors);
        var status = Status(request.Status, StudentStatusCodes.All, StudentStatusCodes.Active, errors);
        RequireId(request.SchoolClassId, "schoolClassId", errors);
        if (errors.Count > 0)
        {
            return Invalid<StudentDetailDto>(errors);
        }

        var created = await repository.CreateStudentAsync(
            new CreateStudentCommand(
                schoolId, code, fullName, request.DateOfBirth, Trim(request.Gender),
                request.AdmissionDate, status, request.SchoolClassId),
            cancellationToken);
        return await StudentResultAsync(schoolId, created, cancellationToken);
    }

    public async Task<ServiceResult<StudentDetailDto>> UpdateStudentAsync(
        ulong schoolId,
        ulong studentId,
        UpdateStudentRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var code = Required(request.Code, "code", 64, errors);
        var fullName = Required(request.FullName, "fullName", 255, errors);
        var status = Status(request.Status, StudentStatusCodes.All, StudentStatusCodes.Active, errors);
        if (request.SchoolClassId == 0)
        {
            errors["schoolClassId"] = ["Lớp học không hợp lệ."];
        }

        if (errors.Count > 0)
        {
            return Invalid<StudentDetailDto>(errors);
        }

        var updated = await repository.UpdateStudentAsync(
            new UpdateStudentCommand(
                schoolId, studentId, code, fullName, request.DateOfBirth, Trim(request.Gender),
                request.AdmissionDate, status, request.SchoolClassId),
            cancellationToken);
        return await StudentResultAsync(schoolId, updated, cancellationToken);
    }

    public async Task<ServiceResult<StudentDetailDto>> TransferStudentClassAsync(
        ulong schoolId,
        ulong studentId,
        TransferStudentClassRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SchoolClassId == 0)
        {
            return Invalid<StudentDetailDto>(new Dictionary<string, string[]>
            {
                ["schoolClassId"] = ["Giá trị là bắt buộc."]
            });
        }

        var transferred = await repository.TransferStudentClassAsync(
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
        var deleted = await repository.DeactivateStudentAsync(
            schoolId, studentId, cancellationToken);
        return await StudentResultAsync(schoolId, deleted, cancellationToken);
    }

    public async Task<ServiceResult<ClassDetailDto>> CreateClassAsync(
        ulong schoolId,
        CreateClassRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var code = Required(request.Code, "code", 64, errors);
        var name = Required(request.Name, "name", 100, errors);
        var status = Status(
            request.Status, SchoolClassStatusCodes.All, SchoolClassStatusCodes.Active, errors);
        RequireId(request.SchoolBranchId, "schoolBranchId", errors);
        RequireId(request.AcademicYearId, "academicYearId", errors);
        RequireId(request.GradeLevelId, "gradeLevelId", errors);
        if (errors.Count > 0)
        {
            return Invalid<ClassDetailDto>(errors);
        }

        var created = await repository.CreateClassAsync(
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
        var errors = new Dictionary<string, string[]>();
        var code = Required(request.Code, "code", 64, errors);
        var name = Required(request.Name, "name", 100, errors);
        var status = Status(
            request.Status, SchoolClassStatusCodes.All, SchoolClassStatusCodes.Active, errors);
        RequireId(request.SchoolBranchId, "schoolBranchId", errors);
        RequireId(request.AcademicYearId, "academicYearId", errors);
        RequireId(request.GradeLevelId, "gradeLevelId", errors);
        if (errors.Count > 0)
        {
            return Invalid<ClassDetailDto>(errors);
        }

        var updated = await repository.UpdateClassAsync(
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
        var deleted = await repository.DeactivateClassAsync(schoolId, classId, cancellationToken);
        return await ClassResultAsync(schoolId, deleted, cancellationToken);
    }

    // Writes return only the id; the read service renders the same shape the GET routes return.
    private async Task<ServiceResult<StudentDetailDto>> StudentResultAsync(
        ulong schoolId,
        DirectoryWriteResult<ulong> write,
        CancellationToken cancellationToken) =>
        write.Status is DirectoryWriteStatus.Success
            ? await readService.GetStudentAsync(
                DirectoryScope.ForSchool(schoolId), write.Value, cancellationToken)
            : MapFailure<StudentDetailDto>(write.Status);

    private async Task<ServiceResult<ClassDetailDto>> ClassResultAsync(
        ulong schoolId,
        DirectoryWriteResult<ulong> write,
        CancellationToken cancellationToken) =>
        write.Status is DirectoryWriteStatus.Success
            ? await readService.GetClassAsync(
                DirectoryScope.ForSchool(schoolId), write.Value,
                new PageQuery(1, RosterPageSize), cancellationToken)
            : MapFailure<ClassDetailDto>(write.Status);

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static string Required(
        string? value,
        string field,
        int maxLength,
        Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            errors[field] = ["Giá trị là bắt buộc."];
        }
        else if (trimmed.Length > maxLength)
        {
            errors[field] = [$"Giá trị tối đa {maxLength} ký tự."];
        }

        return trimmed;
    }

    private static string Status(
        string? value,
        IReadOnlyCollection<string> allowed,
        string fallback,
        Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(trimmed))
        {
            return fallback;
        }

        if (!allowed.Contains(trimmed))
        {
            errors["status"] = [$"Trạng thái phải là một trong: {string.Join(", ", allowed)}."];
        }

        return trimmed;
    }

    private static void RequireId(ulong value, string field, Dictionary<string, string[]> errors)
    {
        if (value == 0)
        {
            errors[field] = ["Giá trị là bắt buộc."];
        }
    }

    private static ulong? NullableId(ulong? value) => value is 0 or null ? null : value;

    private static ServiceResult<T> Invalid<T>(Dictionary<string, string[]> errors) =>
        ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.Validation, "Dữ liệu gửi lên không hợp lệ.", errors);

    private static ServiceResult<T> MapFailure<T>(DirectoryWriteStatus status) => status switch
    {
        DirectoryWriteStatus.SchoolNotFound => ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.SchoolNotFound, "Không tìm thấy trường."),
        DirectoryWriteStatus.StudentNotFound => ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.StudentNotFound, "Không tìm thấy học sinh."),
        DirectoryWriteStatus.ClassNotFound => ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.ClassNotFound, "Không tìm thấy lớp học."),
        DirectoryWriteStatus.DuplicateStudentCode => ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.StudentCodeDuplicate, "Mã học sinh đã tồn tại."),
        DirectoryWriteStatus.DuplicateClassCode => ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.ClassCodeDuplicate,
            "Mã lớp đã tồn tại trong cơ sở và năm học này."),
        DirectoryWriteStatus.TeacherAlreadyHomeroom => ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.TeacherAlreadyHomeroom,
            "Giáo viên đang chủ nhiệm một lớp khác."),
        DirectoryWriteStatus.ClassHasActiveStudents => ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.ClassHasActiveStudents,
            "Lớp còn học sinh đang theo học. Vui lòng chuyển lớp cho học sinh trước."),
        DirectoryWriteStatus.BranchNotInSchool => Field<T>(
            "schoolBranchId", "Cơ sở không thuộc trường này."),
        DirectoryWriteStatus.AcademicYearInvalid => Field<T>(
            "academicYearId",
            "Năm học không hợp lệ với trường này, hoặc lớp đã có học sinh nên không đổi được năm học."),
        DirectoryWriteStatus.StudentNotEnrolledInYear => Field<T>(
            "schoolClassId", "Học sinh chưa có lớp trong năm học của lớp đích."),
        DirectoryWriteStatus.UseClassTransfer => Field<T>(
            "schoolClassId",
            "Học sinh đã có lớp trong năm học này. Dùng chức năng chuyển lớp để đổi lớp."),
        DirectoryWriteStatus.InvalidEffectiveDate => Field<T>(
            "effectiveOn", "Ngày chuyển không được trước ngày bắt đầu học lớp hiện tại."),
        _ => Field<T>("gradeLevelId", "Khối lớp không tồn tại.")
    };

    private static ServiceResult<T> Field<T>(string field, string message) =>
        ServiceResult<T>.Failure(
            SchoolDirectoryErrorCodes.Validation,
            "Dữ liệu gửi lên không hợp lệ.",
            new Dictionary<string, string[]> { [field] = [message] });
}
