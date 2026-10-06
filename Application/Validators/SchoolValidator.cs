using Application.DTOs;
using Application.Common;

namespace Application.Validators;

public static class SchoolValidator
{
    public static ServiceResult<SchoolListItem>? ValidateCreate(CreateSchoolRequest request, bool isCodeConflict)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<SchoolListItem>.Failure("VALIDATION_ERROR", "Tên trường không được để trống.");

        if (string.IsNullOrWhiteSpace(request.Code))
            return ServiceResult<SchoolListItem>.Failure("VALIDATION_ERROR", "Mã trường không được để trống.");

        if (isCodeConflict)
            return ServiceResult<SchoolListItem>.Failure(
                "SCHOOL_CODE_CONFLICT",
                $"Mã trường '{request.Code.Trim().ToUpperInvariant()}' đã được sử dụng.");
        
        return null;
    }

    public static ServiceResult<SchoolListItem>? ValidateUpdate(UpdateSchoolRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<SchoolListItem>.Failure("VALIDATION_ERROR", "Tên trường không được để trống.");

        return null;
    }
}
