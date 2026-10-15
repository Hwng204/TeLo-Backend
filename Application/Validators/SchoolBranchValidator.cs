using Application.DTOs;
using Application.Common;

namespace Application.Validators;

public static class SchoolBranchValidator
{
    public static ServiceResult<SchoolBranchDetailDto>? ValidateCreate(CreateSchoolBranchRequest request) =>
        Validate(request.Name, request.Logo);

    public static ServiceResult<SchoolBranchDetailDto>? ValidateUpdate(UpdateSchoolBranchRequest request) =>
        Validate(request.Name, request.Logo);

    private static ServiceResult<SchoolBranchDetailDto>? Validate(string? name, string? logo)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ServiceResult<SchoolBranchDetailDto>.Failure("VALIDATION_ERROR", "Tên phân hiệu không được để trống.");

        // branch.logo is VARCHAR(500) in v2.
        if (logo?.Trim().Length > 500)
            return ServiceResult<SchoolBranchDetailDto>.Failure("VALIDATION_ERROR", "Logo tối đa 500 ký tự.");

        return null;
    }
}
