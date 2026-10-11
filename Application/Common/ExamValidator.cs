using Application.DTOs;

namespace Application.Common;

public sealed record ExamValidationResult(
    IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class ExamValidator
{
    private const int MaxNameLength = 255;
    private static readonly IReadOnlySet<string> SortFields = new HashSet<string>(
        ["name", "startDate", "endDate", "status", "semester", "schoolBranch"],
        StringComparer.OrdinalIgnoreCase);

    public static ExamValidationResult Validate(CreateExamRequest request) =>
        ValidateWrite(
            request.SemesterId,
            request.SchoolBranchId,
            request.GradeLevelId,
            request.SubjectId,
            request.Name,
            request.StartDate,
            request.StartTime,
            request.EndDate,
            request.EndTime,
            null,
            false);

    public static ExamValidationResult Validate(UpdateExamRequest request) =>
        ValidateWrite(
            request.SemesterId,
            request.SchoolBranchId,
            request.GradeLevelId,
            request.SubjectId,
            request.Name,
            request.StartDate,
            request.StartTime,
            request.EndDate,
            request.EndTime,
            request.Status,
            true);

    public static ExamValidationResult Validate(ExamListQuery query)
    {
        var errors = NewErrors();

        if (query.Keyword?.Trim().Length > MaxNameLength)
        {
            Add(errors, "keyword", $"Từ khóa không được vượt quá {MaxNameLength} ký tự.");
        }

        if (query.Status is not null && !ExamStatusCodes.All.Contains(query.Status.Trim()))
        {
            Add(errors, "status", "Trạng thái kỳ thi không hợp lệ.");
        }

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate > query.ToDate)
        {
            Add(errors, "toDate", "Ngày kết thúc bộ lọc phải lớn hơn hoặc bằng ngày bắt đầu.");
        }

        if (query.PageNumber < 1)
        {
            Add(errors, "pageNumber", "Số trang phải lớn hơn hoặc bằng 1.");
        }

        if (query.PageSize is < 1 or > 100)
        {
            Add(errors, "pageSize", "Kích thước trang phải từ 1 đến 100.");
        }

        if (query.SortBy is not null && !SortFields.Contains(query.SortBy.Trim()))
        {
            Add(errors, "sortBy", "Trường sắp xếp không hợp lệ.");
        }

        if (query.SortDirection is not null &&
            !query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !query.SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            Add(errors, "sortDirection", "Chiều sắp xếp phải là asc hoặc desc.");
        }

        return Result(errors);
    }

    private static ExamValidationResult ValidateWrite(
        ulong semesterId,
        ulong schoolBranchId,
        ulong gradeLevelId,
        ulong subjectId,
        string? name,
        DateOnly? startDate,
        TimeOnly? startTime,
        DateOnly? endDate,
        TimeOnly? endTime,
        string? status,
        bool requireStatus)
    {
        var errors = NewErrors();
        var normalizedName = name?.Trim() ?? string.Empty;

        if (semesterId == 0)
        {
            Add(errors, "semesterId", "Học kỳ là bắt buộc.");
        }

        if (schoolBranchId == 0)
        {
            Add(errors, "schoolBranchId", "Cơ sở trường là bắt buộc.");
        }

        if (gradeLevelId == 0)
        {
            Add(errors, "gradeLevelId", "Khối thi là bắt buộc.");
        }

        if (subjectId == 0)
        {
            Add(errors, "subjectId", "Môn thi là bắt buộc.");
        }

        if (normalizedName.Length == 0)
        {
            Add(errors, "name", "Tên kỳ thi là bắt buộc.");
        }
        else if (normalizedName.Length > MaxNameLength)
        {
            Add(errors, "name", $"Tên kỳ thi không được vượt quá {MaxNameLength} ký tự.");
        }

        if (!startDate.HasValue)
        {
            Add(errors, "startDate", "Ngày bắt đầu kỳ thi là bắt buộc.");
        }

        if (!startTime.HasValue)
        {
            Add(errors, "startTime", "Giờ bắt đầu kỳ thi là bắt buộc.");
        }

        if (!endDate.HasValue)
        {
            Add(errors, "endDate", "Ngày kết thúc kỳ thi là bắt buộc.");
        }

        if (!endTime.HasValue)
        {
            Add(errors, "endTime", "Giờ kết thúc kỳ thi là bắt buộc.");
        }

        if (startDate.HasValue && startTime.HasValue && endDate.HasValue && endTime.HasValue &&
            startDate.Value.ToDateTime(startTime.Value) >= endDate.Value.ToDateTime(endTime.Value))
        {
            Add(errors, "endDate", "Thời gian kết thúc phải sau thời gian bắt đầu.");
        }

        if (requireStatus && string.IsNullOrWhiteSpace(status))
        {
            Add(errors, "status", "Trạng thái kỳ thi là bắt buộc.");
        }
        else if (status is not null && !ExamStatusCodes.All.Contains(status.Trim()))
        {
            Add(errors, "status", "Trạng thái kỳ thi không hợp lệ.");
        }

        return Result(errors);
    }

    private static Dictionary<string, List<string>> NewErrors() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static void Add(IDictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var fieldErrors))
        {
            fieldErrors = [];
            errors[field] = fieldErrors;
        }

        fieldErrors.Add(message);
    }

    private static ExamValidationResult Result(Dictionary<string, List<string>> errors) =>
        new(errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase));
}
