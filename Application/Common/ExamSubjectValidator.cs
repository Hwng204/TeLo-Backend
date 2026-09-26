using Application.DTOs;

namespace Application.Common;

public static class ExamSubjectValidator
{
    public static ExamValidationResult Validate(CreateExamSubjectRequest request) =>
        ValidateWrite(request.SubjectId, request.DurationMinutes, null, false);

    public static ExamValidationResult Validate(UpdateExamSubjectRequest request) =>
        ValidateWrite(request.SubjectId, request.DurationMinutes, request.Status, true);

    private static ExamValidationResult ValidateWrite(
        ulong subjectId,
        uint durationMinutes,
        string? status,
        bool requireStatus)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (subjectId == 0)
        {
            Add(errors, "subjectId", "Môn học là bắt buộc.");
        }

        if (durationMinutes == 0)
        {
            Add(errors, "durationMinutes", "Thời lượng thi phải lớn hơn 0 phút.");
        }

        if (requireStatus && string.IsNullOrWhiteSpace(status))
        {
            Add(errors, "status", "Trạng thái môn thi là bắt buộc.");
        }
        else if (status is not null && !ExamSubjectStatusCodes.All.Contains(status.Trim()))
        {
            Add(errors, "status", "Trạng thái môn thi không hợp lệ.");
        }

        return new ExamValidationResult(errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase));
    }

    private static void Add(IDictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var fieldErrors))
        {
            fieldErrors = [];
            errors[field] = fieldErrors;
        }

        fieldErrors.Add(message);
    }
}
