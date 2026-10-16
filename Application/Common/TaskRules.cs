using Application.DTOs;
using Domain.Entities.QuestionBank;

namespace Application.Common;

// Kiểm tra dữ liệu nhiệm vụ không cần DB (GB-52, GB-53). TaskService kiểm tra tiếp người, bài học
// và ma trận trong phân hiệu. `current` là nhiệm vụ đang sửa (null khi tạo mới).
public static class TaskRules
{
    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).DateTime);
    public const string Overdue = "OVERDUE";
    public const int MaxNameLength = 255;
    public const int MaxLessons = 50;
    public const int MaxQuestionsPerLevel = 1000;
    public const int MaxPapers = 20;
    public const int MaxPageSize = 100;

    public static Dictionary<string, string[]> Query(TaskListQuery query)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Page < 1) Add(errors, "page", "Trang không hợp lệ.");
        if (query.PageSize is < 1 or > MaxPageSize) Add(errors, "pageSize", $"Số bản ghi mỗi trang từ 1 đến {MaxPageSize}.");
        if (query.Keyword?.Length > MaxNameLength) Add(errors, "keyword", $"Từ khoá tối đa {MaxNameLength} ký tự.");
        if (!string.IsNullOrEmpty(query.TaskType) && !TaskTypeCodes.All.Contains(query.TaskType))
            Add(errors, "taskType", "Loại nhiệm vụ không hợp lệ.");
        if (!string.IsNullOrEmpty(query.Status) && query.Status != Overdue && !MatrixTaskStatusCodes.All.Contains(query.Status))
            Add(errors, "status", "Trạng thái không hợp lệ.");
        return errors;
    }

    // GB-53: người thực hiện còn việc phải làm (kể cả phải sửa lại sau khi bị từ chối) mà đã qua hạn.
    // Chờ kiểm duyệt (đã gửi xong) và hoàn thành thì không. TaskService.OverdueBefore là cùng quy tắc viết cho câu truy vấn.
    public static bool IsOverdue(string status, DateTime? dueAt, DateOnly today) =>
        status is MatrixTaskStatusCodes.Assigned or MatrixTaskStatusCodes.InProgress or MatrixTaskStatusCodes.NeedsRevision &&
        Before(dueAt, today);

    public static Dictionary<string, string[]> MatrixTask(CreateMatrixTaskRequest request, WorkTask? current, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();
        Name(request.Name, "Nội dung nhiệm vụ", errors);
        if (request.AssignedToUserId == 0) Add(errors, "assignedToUserId", "Chọn Tổ trưởng nhận việc.");
        if (request.AcademicContextId == 0) Add(errors, "academicContextId", "Chọn đủ Môn học và Khối lớp.");
        Due(request.DueAt, current?.DueAt, today, "dueAt", "Hạn hoàn thành", required: false, errors);
        return errors;
    }

    public static Dictionary<string, string[]> ExamTask(SaveExamTaskRequest request, WorkTask? current, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();
        Name(request.Name, "Nội dung nhiệm vụ", errors);
        if (request.ExamMatrixId == 0) Add(errors, "examMatrixId", "Chọn ma trận đã duyệt.");
        if (request.AssignedToUserId == 0) Add(errors, "assignedToUserId", "Chọn Tổ trưởng thực hiện.");
        Due(request.DueAt, current?.DueAt, today, "dueAt", "Hạn hoàn thành", required: true, errors);
        if (request.PaperCount is < 1 or > MaxPapers) Add(errors, "paperCount", $"Số đề cần tạo từ 1 đến {MaxPapers}.");
        return errors;
    }

    public static Dictionary<string, string[]> QuestionTask(SaveQuestionTaskRequest request, WorkTask? current, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();
        Name(request.Name, "Nội dung nhiệm vụ", errors);
        if (request.AssignedToUserId == 0) Add(errors, "assignedToUserId", "Chọn giáo viên biên soạn.");
        Due(request.DueAt, current?.DueAt, today, "dueAt", "Hạn biên soạn", required: true, errors);

        var lessons = request.Lessons ?? [];
        if (lessons.Count == 0) Add(errors, "lessons", "Thêm ít nhất một bài học.");
        else if (lessons.Count > MaxLessons) Add(errors, "lessons", $"Một nhiệm vụ tối đa {MaxLessons} bài học.");
        else if (lessons.Any(lesson => lesson is null || lesson.LessonId == 0))
            Add(errors, "lessons", "Chọn khối lớp, chương và bài học cho mọi thẻ bài học.");
        else if (lessons.Select(lesson => lesson.LessonId).Distinct().Count() != lessons.Count)
            Add(errors, "lessons", "Mỗi bài học chỉ được chọn một lần.");
        else if (lessons.Any(lesson => Levels(lesson).Any(level => level.Count > MaxQuestionsPerLevel)))
            Add(errors, "lessons", $"Mỗi mức nhận thức tối đa {MaxQuestionsPerLevel} câu.");
        else if (lessons.Any(lesson => Levels(lesson).All(level => level.Count == 0)))
            Add(errors, "lessons", "Mỗi bài học cần ít nhất một câu hỏi.");
        return errors;
    }

    // Tên đề do hệ thống đặt theo thứ tự, người dùng không sửa được.
    public static IReadOnlyList<string> PaperNames(int count) =>
        Enumerable.Range(1, count).Select(index => $"Đề {index}").ToArray();

    public static IEnumerable<(string Level, uint Count)> Levels(QuestionTaskLessonRequest lesson) =>
    [
        (MatrixCognitiveLevels.Knowledge, lesson.Knowledge),
        (MatrixCognitiveLevels.Understanding, lesson.Understanding),
        (MatrixCognitiveLevels.Application, lesson.Application)
    ];

    private static void Name(string? name, string label, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(name)) Add(errors, "name", $"{label} là bắt buộc.");
        else if (name.Trim().Length > MaxNameLength) Add(errors, "name", $"{label} tối đa {MaxNameLength} ký tự.");
    }

    // Hạn không được ở quá khứ (GB-52). Khi sửa, hạn cũ giữ nguyên thì không bắt đặt lại, kể cả khi đã quá hạn.
    private static void Due(DateTime? value, DateTime? current, DateOnly today, string key, string label, bool required,
        Dictionary<string, string[]> errors)
    {
        if (value is null)
        {
            if (required) Add(errors, key, $"{label} là bắt buộc.");
            return;
        }

        if (value.Value.Date != current?.Date && Before(value, today)) Add(errors, key, $"{label} không được ở trong quá khứ.");
    }

    private static bool Before(DateTime? value, DateOnly today) => value is { } date && DateOnly.FromDateTime(date) < today;

    private static void Add(Dictionary<string, string[]> errors, string key, string message) =>
        errors[key] = errors.TryGetValue(key, out var existing) ? [.. existing, message] : [message];
}
