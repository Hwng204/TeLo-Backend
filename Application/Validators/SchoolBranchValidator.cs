using Application.DTOs;
using Application.Common;

namespace Application.Validators;

public static class SchoolBranchValidator
{
    public static ServiceResult<SchoolBranchDetailDto>? ValidateCreate(CreateSchoolBranchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<SchoolBranchDetailDto>.Failure("VALIDATION_ERROR", "Tên phân hiệu không được để trống.");

        return null;
    }

    public static ServiceResult<SchoolBranchDetailDto>? ValidateUpdate(UpdateSchoolBranchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<SchoolBranchDetailDto>.Failure("VALIDATION_ERROR", "Tên phân hiệu không được để trống.");

        return null;
    }
}
