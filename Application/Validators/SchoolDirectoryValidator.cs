using System.Collections.Generic;
using Application.DTOs;
using Application.Common;
using Domain.Entities.Identity;
using Domain.Entities.Organization;

namespace Application.Validators;

public static class SchoolDirectoryValidator
{
    public static Dictionary<string, string[]> ValidateCreateStudent(CreateStudentRequest request, out string code, out string fullName, out string status)
    {
        var errors = new Dictionary<string, string[]>();
        code = Required(request.Code, "code", 64, errors);
        fullName = Required(request.FullName, "fullName", 255, errors);
        status = Status(request.Status, StudentStatusCodes.All, StudentStatusCodes.Active, errors);
        RequireId(request.SchoolClassId, "schoolClassId", errors);
        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdateStudent(UpdateStudentRequest request, out string code, out string fullName, out string status)
    {
        var errors = new Dictionary<string, string[]>();
        code = Required(request.Code, "code", 64, errors);
        fullName = Required(request.FullName, "fullName", 255, errors);
        status = Status(request.Status, StudentStatusCodes.All, StudentStatusCodes.Active, errors);
        RequireId(request.SchoolClassId, "schoolClassId", errors);
        return errors;
    }

    public static Dictionary<string, string[]> ValidateTransferStudentClass(TransferStudentClassRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        RequireId(request.SchoolClassId, "schoolClassId", errors);
        return errors;
    }

    public static Dictionary<string, string[]> ValidateCreateClass(CreateClassRequest request, out string name, out string code, out string status)
    {
        var errors = new Dictionary<string, string[]>();
        name = Required(request.Name, "name", 100, errors);
        code = OptionalCode(request.Code, name, errors);
        status = Status(request.Status, SchoolClassStatusCodes.All, SchoolClassStatusCodes.Active, errors);
        RequireId(request.SchoolBranchId, "schoolBranchId", errors);
        RequireId(request.AcademicYearId, "academicYearId", errors);
        RequireId(request.GradeLevelId, "gradeLevelId", errors);
        return errors;
    }

    public static Dictionary<string, string[]> ValidateUpdateClass(UpdateClassRequest request, out string name, out string code, out string status)
    {
        var errors = new Dictionary<string, string[]>();
        name = Required(request.Name, "name", 100, errors);
        code = OptionalCode(request.Code, string.Empty, errors);
        status = Status(request.Status, SchoolClassStatusCodes.All, SchoolClassStatusCodes.Active, errors);
        RequireId(request.SchoolBranchId, "schoolBranchId", errors);
        RequireId(request.AcademicYearId, "academicYearId", errors);
        RequireId(request.GradeLevelId, "gradeLevelId", errors);
        return errors;
    }

    public static string OptionalCode(string? value, string fallback, Dictionary<string, string[]> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length > 64)
        {
            errors["code"] = ["Giá trị tối đa 64 ký tự."];
        }

        return trimmed.Length == 0 ? fallback : trimmed;
    }

    public static string Required(
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

    public static string Status(
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

    public static void RequireId(ulong? value, string field, Dictionary<string, string[]> errors)
    {
        if (value is null || value == 0)
        {
            errors[field] = ["Giá trị là bắt buộc."];
        }
    }
}
