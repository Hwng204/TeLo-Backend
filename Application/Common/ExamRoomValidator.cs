using System.Text.RegularExpressions;
using Application.DTOs;

namespace Application.Common;

public static partial class ExamRoomValidator
{
    private const int MaxCodeLength = 50;
    private const int MaxPrefixLength = 40;

    public static ExamValidationResult Validate(CreateExamRoomRequest request) =>
        ValidateWrite(request.Code, request.RoomId, request.CandidateLimit);

    public static ExamValidationResult Validate(UpdateExamRoomRequest request) =>
        ValidateWrite(request.Code, request.RoomId, request.CandidateLimit);

    public static ExamValidationResult Validate(CreateExamRoomsRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var prefix = request.CodePrefix?.Trim() ?? string.Empty;
        var roomIds = request.RoomIds ?? [];

        if (request.RoomCount != 2)
        {
            Add(errors, "roomCount", "Kỳ thi phải sử dụng đúng 2 phòng máy.");
        }

        if (request.CandidateLimit == 0)
        {
            Add(errors, "candidateLimit", "Số học sinh mỗi phòng phải lớn hơn 0.");
        }

        if (prefix.Length == 0)
        {
            Add(errors, "codePrefix", "Tiền tố mã phòng thi là bắt buộc.");
        }
        else if (prefix.Length > MaxPrefixLength)
        {
            Add(errors, "codePrefix", $"Tiền tố mã phòng thi không được vượt quá {MaxPrefixLength} ký tự.");
        }
        else if (!CodePrefixPattern().IsMatch(prefix))
        {
            Add(errors, "codePrefix", "Tiền tố chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.");
        }

        if (roomIds.Count != request.RoomCount)
        {
            Add(errors, "roomIds", "Số phòng máy được chọn phải bằng số phòng thi.");
        }
        else if (roomIds.Any(id => id == 0) || roomIds.Distinct().Count() != roomIds.Count)
        {
            Add(errors, "roomIds", "Danh sách phòng máy không hợp lệ hoặc bị trùng.");
        }

        return new ExamValidationResult(errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase));
    }

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
            Add(errors, "code", "Mã phòng thi chỉ gồm chữ, số, dấu gạch ngang hoặc gạch dưới.");
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

    [GeneratedRegex(@"^[A-Z0-9]+(?:[-_][A-Z0-9]+)*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^[A-Z0-9]+(?:[-_][A-Z0-9]+)*[-_]?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodePrefixPattern();
}
