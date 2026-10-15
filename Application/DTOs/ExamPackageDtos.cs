using System.Text.Json.Serialization;

namespace Application.DTOs;

public sealed record ExamPackageQuery(int Page = 1, int PageSize = 10, string? Keyword = null, string? Status = null,
    string? Purpose = null, ulong? AcademicYearId = null, ulong? SubjectId = null, ulong? GradeLevelId = null);
public sealed record GenerateExamPackageRequest(string? Name, ulong MatrixId);
public sealed record RenameExamPackageRequest([property: JsonRequired] int Version, string? Name);
public sealed record AssignExamReviewRequest([property: JsonRequired] int Version, ulong ReviewerUserId, DateTime? DueAt);
public sealed record ReplaceExamQuestionRequest([property: JsonRequired] int Version, ulong QuestionId);
public sealed record ExamPackageCommand([property: JsonRequired] int Version, string? Comment = null);
public sealed record ExamPackageOption(ulong Id, string Name, string SubjectName, string GradeLevelName, string AcademicYearName,
    ulong AcademicYearId, ulong SubjectId, ulong GradeLevelId, string Status);
public sealed record ExamPackageOptions(bool CanCreate, IReadOnlyList<ExamPackageOption> Matrices, IReadOnlyList<TaskPersonOption> Reviewers,
    IReadOnlyList<BankFilterOption>? Years = null);
public sealed record ExamPackageListItem(ulong Id, string Name, string Status, string Purpose, ulong MatrixId,
    string MatrixName, string SubjectName, string GradeLevelName, string AcademicYearName, int QuestionCount,
    string AuthorName, string? ReviewerName, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? ApprovedAt);
public sealed record ExamPackageCell(ulong Id, string LessonTitle, string CognitiveLevel, string QuestionType,
    uint RequiredCount, decimal Percentage, decimal Score);
public sealed record ExamPackageQuestion(ulong Id, ulong DetailId, QuestionItem Question);
public sealed record ExamPackageHistoryItem(ulong Id, int Version, string EventCode, string ActorName, DateTime CreatedAt,
    string? Comment, ulong? OldQuestionId, ulong? NewQuestionId);
public sealed record ExamPackageDetail(ExamPackageListItem Package, int Version, int TotalScore, ulong? TaskId,
    ulong? ReviewerUserId, DateTime? DueAt, string? ApprovedByName, ulong? SourceExamSetId,
    bool CanViewTask, bool CanRename, bool CanAssign, bool CanReplace, bool CanSubmitReview, bool CanReturnReview,
    bool CanFinalize, bool CanDelete, bool CanConvertToPractice,
    IReadOnlyList<ExamPackageCell> Cells, IReadOnlyList<ExamPackageQuestion> Questions, IReadOnlyList<ExamPackageHistoryItem> History);
