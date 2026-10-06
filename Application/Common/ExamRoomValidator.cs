using System.Text.RegularExpressions;
using Application.DTOs;

namespace Application.Common;

public static partial class ExamRoomValidator
{
    private const int MaxCodeLength = 50;

    public static ExamValidationResult Validate(CreateExamRoomRequest request) =>
        ValidateWrite(request.Code, request.RoomId, request.CandidateLimit);

    public static ExamValidationResult Validate(UpdateExamRoomRequest request) =>
        ValidateWrite(request.Code, request.RoomId, request.CandidateLimit);

    private static ExamValidationResult ValidateWrite(string? code, ulong roomId, uint candidateLimit)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var normalizedCode = code?.Trim() ?? string.Empty;

        if (normalizedCode.Length == 0)
        {
            Add(errors, "code", "Mã phòng thi là bắt buộc.");
        }
        else if (normalizedCode.Length > MaxCodeLength)
        {
            Add(errors, "code", $"Mã phòng thi không được vượt quá {MaxCodeLength} ký tự.");
        }
        else if (!CodePattern().IsMatch(normalizedCode))
        {
            Add(errors, "code", "Mã phòng thi phải có dạng k5p01.");
        }

        if (roomId == 0)
        {
            Add(errors, "roomId", "Phòng học là bắt buộc.");
        }

        if (candidateLimit == 0)
        {
            Add(errors, "candidateLimit", "Sức chứa phòng thi phải lớn hơn 0.");
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

    [GeneratedRegex(@"^k[1-5]p\d{2,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
