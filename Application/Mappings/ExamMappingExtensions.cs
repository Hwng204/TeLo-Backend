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
            NormalizeOptional(query.SortDirection)?.ToLowerInvariant() ?? "desc");

    public static ExamListItem ToDto(this ExamListData exam) =>
        new(
            exam.Id,
            exam.Name,
            new ExamSemesterSummary(exam.SemesterId, exam.SemesterName),
            new ExamSchoolBranchSummary(
                exam.SchoolBranchId,
                exam.SchoolBranchCode,
                exam.SchoolBranchName),
            exam.StartDate,
            exam.EndDate,
            exam.Status);

    public static ExamDetailDto ToDto(this ExamDetailData exam) =>
        new(
            exam.Id,
            exam.Name,
            new ExamSemesterSummary(exam.SemesterId, exam.SemesterName),
            new ExamSchoolBranchSummary(
                exam.SchoolBranchId,
                exam.SchoolBranchCode,
                exam.SchoolBranchName),
            exam.StartDate,
            exam.EndDate,
            exam.Status,
            exam.SubjectCount,
            exam.SessionCount,
            exam.RoomCount,
            exam.CandidateCount,
            exam.ProctorCount);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
