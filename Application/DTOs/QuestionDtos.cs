using System.Text.Json.Serialization;
namespace Application.DTOs;

public sealed record QuestionActor(ulong UserId, bool Teacher, bool TeamLead, bool Pht, bool Principal);
public sealed record SaveQuestionRequest([property: JsonRequired] int Version, ulong DetailId, string? Content, string[]? Options, string? CorrectOption, string? Explanation);
public sealed record QuestionCommand([property: JsonRequired] int Version);
public sealed record ReviewQuestionRequest([property: JsonRequired] int Version, string? Comment, IReadOnlyList<QuestionReviewNote>? Notes);
public sealed record QuestionReviewNote(ulong QuestionId, string Comment);
public sealed record QuestionItem(ulong Id, ulong DetailId, string Content, string[] Options, string? CorrectOption, string? Explanation, bool IsComplete);
public sealed record QuestionTarget(ulong DetailId, ulong LessonId, string GradeName, string ChapterCode, string LessonCode, string LessonTitle,
    string CognitiveLevel, uint RequiredCount, int Written, int ValidCount, string? ChapterTitle = null);
public sealed record QuestionSubmission(ulong Id, int Number, DateTime SubmittedAt, string Status, string? Comment, DateTime? ReviewedAt,
    IReadOnlyList<QuestionReviewNote> Notes, IReadOnlyList<QuestionItem> Questions);
public sealed record QuestionPackage(ulong TaskId, string Name, string Status, int Version, DateTime? DueAt,
    string AuthorName, string ReviewerName, bool CanEdit, bool CanSubmit, bool CanReview,
    IReadOnlyList<QuestionTarget> Targets, IReadOnlyList<QuestionItem> Questions, IReadOnlyList<QuestionSubmission> Submissions);
public sealed record QuestionBankQuery(int Page = 1, int PageSize = 10, string BankType = "exam", string? Keyword = null,
    ulong? SubjectId = null, ulong? GradeLevelId = null, ulong? ChapterId = null, ulong? LessonId = null, string? CognitiveLevel = null,
    ulong? MatrixId = null, ulong? MatrixDetailId = null);
public sealed record BankQuestion(QuestionItem Question, string BankType, ulong LessonId, string LessonTitle, string CognitiveLevel,
    string SubjectName, string GradeName, string AuthorName, DateTime? ApprovedAt, bool CanMoveToPractice, DateTime? PracticeAt, string? PracticeReason);
public sealed record BankFilterOption(ulong Id, string Name, ulong? ParentId = null);
public sealed record QuestionBankOptions(IReadOnlyList<BankFilterOption> Subjects, IReadOnlyList<BankFilterOption> Grades,
    IReadOnlyList<BankFilterOption> Chapters, IReadOnlyList<BankFilterOption> Lessons);
public sealed record MatrixQuestionAvailability(ulong DetailId, ulong LessonId, string CognitiveLevel, uint RequiredCount, int AvailableCount);
