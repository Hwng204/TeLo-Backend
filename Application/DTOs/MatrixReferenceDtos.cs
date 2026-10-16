using Infrastructure.Models;

namespace Application.DTOs;

public sealed record MatrixReferenceData(
    IReadOnlyList<MatrixAcademicContextOption> AcademicContexts,
    IReadOnlyList<MatrixLessonOption> Lessons,
    IReadOnlyList<MatrixTeamLeadOption> TeamLeads);
