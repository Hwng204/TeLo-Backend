namespace Application.DTOs;

public sealed record CreateExamSubjectRequest(
    ulong SubjectId,
    uint DurationMinutes);

public sealed record UpdateExamSubjectRequest(
    ulong SubjectId,
    uint DurationMinutes,
    string Status);

public sealed record ExamSubjectSubjectSummary(
    ulong Id,
    string Name);

public sealed record ExamSubjectDto(
    ulong Id,
    ulong ExamId,
    ExamSubjectSubjectSummary Subject,
    uint DurationMinutes,
    string Status,
    DateTime? ResultPublishedAt,
    ulong? ResultPublishedByUserId,
    int GradeLevelCount);
