using Application.DTOs;
using Infrastructure.Repositories.Interface;

namespace Application.Mappings;

public static class ExamMappingExtensions
{
    public static ExamListFilter ToFilter(this ExamListQuery query) =>
        new(
            NormalizeOptional(query.Keyword),
            query.SemesterId,
            query.SchoolBranchId,
            NormalizeOptional(query.Status)?.ToUpperInvariant(),
            query.FromDate,
            query.ToDate,
            query.PageNumber,
            query.PageSize,
            NormalizeOptional(query.SortBy) ?? "startDate",
            NormalizeOptional(query.SortDirection)?.ToLowerInvariant() ?? "desc",
            query.AcademicYearId);

    public static ExamListItem ToDto(this ExamListData exam) =>
        new(
            exam.Id,
            exam.Name,
            new ExamSemesterSummary(exam.SemesterId, exam.SemesterName),
            new ExamAcademicYearSummary(exam.AcademicYearId, exam.AcademicYearName),
            exam.GradeLevelId.HasValue
                ? new ExamCatalogSummary(exam.GradeLevelId.Value, exam.GradeLevelName ?? string.Empty)
                : null,
            exam.SubjectId.HasValue
                ? new ExamCatalogSummary(exam.SubjectId.Value, exam.SubjectName ?? string.Empty)
                : null,
            new ExamSchoolBranchSummary(
                exam.SchoolBranchId,
                exam.SchoolBranchCode,
                exam.SchoolBranchName),
            exam.StartDate,
            exam.StartTime,
            exam.EndDate,
            exam.EndTime,
            exam.Status);

    public static ExamDetailDto ToDto(this ExamDetailData exam) =>
        new(
            exam.Id,
            exam.Name,
            new ExamSemesterSummary(exam.SemesterId, exam.SemesterName),
            new ExamAcademicYearSummary(exam.AcademicYearId, exam.AcademicYearName),
            exam.GradeLevelId.HasValue
                ? new ExamCatalogSummary(exam.GradeLevelId.Value, exam.GradeLevelName ?? string.Empty)
                : null,
            exam.SubjectId.HasValue
                ? new ExamCatalogSummary(exam.SubjectId.Value, exam.SubjectName ?? string.Empty)
                : null,
            new ExamSchoolBranchSummary(
                exam.SchoolBranchId,
                exam.SchoolBranchCode,
                exam.SchoolBranchName),
            exam.StartDate,
            exam.StartTime,
            exam.EndDate,
            exam.EndTime,
            exam.Status,
            exam.SubjectCount,
            exam.SessionCount,
            exam.RoomCount,
            exam.CandidateCount,
            exam.ProctorCount);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
