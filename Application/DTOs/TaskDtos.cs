using Application.Common;
using Domain.Entities.QuestionBank;

namespace Application.DTOs;

// Vai trò lấy từ token; phân hiệu lấy từ tài khoản trong DB, không bao giờ nhận từ request.
public sealed record TaskActor(ulong UserId, bool IsPht, bool IsTeamLead, bool IsPrincipal = false);

// Status nhận thêm "OVERDUE" (Quá hạn) ngoài các mã trạng thái thật.
public sealed record TaskListQuery(
    int Page = 1,
    int PageSize = 10,
    string? Keyword = null,
    string? TaskType = null,
    string? Status = null);

public sealed record TaskListItem(
    ulong Id,
    string TaskType,
    string Name,
    string Status,
    bool IsOverdue,
    DateTime? DueAt,
    MatrixPerson? Performer,
    MatrixPerson? Reviewer,
    bool CanEdit,
    bool CanDelete)
{
    public string TaskTypeLabel => TaskTypeCodes.Label(TaskType);
    public string StatusLabel => MatrixTaskStatusCodes.Label(Status);
}

public sealed record TaskDetail(
    ulong Id,
    string TaskType,
    string Name,
    string? Description,
    string Status,
    bool IsOverdue,
    // Người thực hiện đã tạo sản phẩm (ma trận, đề, câu hỏi) hoặc nhiệm vụ đã rời "Đã giao".
    bool IsStarted,
    bool CanEdit,
    bool CanDelete,
    MatrixPerson? CreatedBy,
    DateTime CreatedAt,
    MatrixPerson? Performer,
    MatrixPerson? Reviewer,
    DateTime? DueAt,
    TaskScope? Scope,
    TaskMatrixItem? Matrix,
    TaskExamInfo? Exam,
    IReadOnlyList<TaskLessonItem> Lessons)
{
    public string TaskTypeLabel => TaskTypeCodes.Label(TaskType);
    public string StatusLabel => MatrixTaskStatusCodes.Label(Status);
}

public sealed record TaskScope(
    ulong AcademicContextId,
    string SubjectName,
    string GradeLevelName,
    string AcademicYearName,
    ulong? SemesterId,
    string? SemesterName,
    ulong? AcademicYearId = null,
    ulong? SubjectId = null,
    ulong? GradeLevelId = null);

// Ma trận Tổ trưởng lập cho nhiệm vụ. Bản nháp chỉ Tổ trưởng thấy (GB-47): người khác nhận Id và Name null.
// RejectComment/RejectedAt là lần từ chối gần nhất (null khi chưa bị từ chối), ai xem được nhiệm vụ cũng thấy.
public sealed record TaskMatrixItem(ulong? Id, string? Name, string Status, DateTime UpdatedAt,
    string? RejectComment = null, DateTime? RejectedAt = null)
{
    public string StatusLabel => MatrixStatusCodes.Label(Status);
}

// Ma trận đã duyệt dùng để tạo đề; kỳ thi chọn đề sau, khi tạo kỳ thi.
public sealed record TaskExamInfo(
    ulong MatrixId,
    string MatrixName,
    string SubjectName,
    string GradeLevelName,
    string AcademicYearName,
    IReadOnlyList<TaskPaperItem> Papers);

// shortcut: chưa có chức năng soạn từng đề nên đề nào cũng "Chưa tạo"; tính từ đề đã soạn khi chức năng đó có.
public sealed record TaskPaperItem(string Name)
{
    public string Status => "NOT_CREATED";
    public string StatusLabel => "Chưa tạo";
    public DateTime? UpdatedAt => null;
}

public sealed record TaskLessonItem(
    ulong LessonId,
    ulong ChapterId,
    ulong GradeLevelId,
    string GradeLevelName,
    string ChapterCode,
    string ChapterTitle,
    string LessonCode,
    string LessonTitle,
    uint Knowledge,
    uint Understanding,
    uint Application,
    // Câu hỏi giáo viên đã soạn cho bài; PendingReview: đã gửi mà chưa được kiểm duyệt.
    int Written,
    int PendingReview)
{
    public uint Total => Knowledge + Understanding + Application;
}

// PaperCount: số đề cần tạo (1–20); hệ thống tự đặt tên Đề 1 … Đề N.
public sealed record SaveExamTaskRequest(
    string? Name,
    ulong ExamMatrixId,
    ulong AssignedToUserId,
    DateTime? DueAt,
    int PaperCount);

public sealed record SaveQuestionTaskRequest(
    string? Name,
    ulong AssignedToUserId,
    DateTime? DueAt,
    IReadOnlyList<QuestionTaskLessonRequest>? Lessons);

// Số câu trắc nghiệm cần soạn cho từng mức nhận thức của một bài học.
public sealed record QuestionTaskLessonRequest(ulong LessonId, uint Knowledge, uint Understanding, uint Application);

// Lựa chọn cho các form giao nhiệm vụ, chỉ trong phân hiệu của người gọi.
public sealed record TaskOptions(
    IReadOnlyList<TaskPersonOption> Teachers,
    IReadOnlyList<TaskPersonOption> TeamLeads,
    IReadOnlyList<TaskMatrixOption> Matrices);

public sealed record TaskPersonOption(ulong Id, string FullName, string Username);

public sealed record TaskMatrixOption(ulong Id, string Name, string SubjectName, string GradeLevelName, string AcademicYearName);
