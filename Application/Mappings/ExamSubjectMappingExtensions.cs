using Application.DTOs;
using Infrastructure.Repositories.Interface;

namespace Application.Mappings;

public static class ExamSubjectMappingExtensions
{
    public static ExamSubjectDto ToDto(this ExamSubjectData item) =>
        new(
            item.Id,
            item.ExamId,
            new ExamSubjectSubjectSummary(item.SubjectId, item.SubjectName),
            item.DurationMinutes,
            item.Status,
            item.ResultPublishedAt,
            item.ResultPublishedByUserId,
            item.GradeLevelCount);
}
