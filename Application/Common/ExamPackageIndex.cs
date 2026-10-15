using Domain.Entities.QuestionBank;
using Infrastructure.Context;

namespace Application.Common;

public static class ExamPackageIndex
{
    // Search only the latest chunk-zero envelope for each set. Filtering on any
    // historical version would return a set with a superseded purpose/year.
    public static IQueryable<ExamSet> Filter(IQueryable<ExamSet> query, ApplicationDbContext db, string? purpose, ulong? year)
    {
        if (string.IsNullOrEmpty(purpose) && year is null) return query;
        return query.Where(set => db.TaskHistories.Any(history => history.TaskId == set.TaskId &&
            history.Content.StartsWith(TaskJournalCodec.Prefix) && history.Content.Contains("\"index\":0,") &&
            history.Content.Contains("\"stream\":\"exam:" + set.Id + "\"") &&
            (purpose == null || purpose == "" || history.Content.Contains("\"purpose\":\"" + purpose + "\"")) &&
            (year == null || history.Content.Contains("\"academicYearId\":" + year + "}")) &&
            !db.TaskHistories.Any(newer => newer.TaskId == history.TaskId && newer.Id > history.Id &&
                newer.Content.StartsWith(TaskJournalCodec.Prefix) && newer.Content.Contains("\"index\":0,") &&
                newer.Content.Contains("\"stream\":\"exam:" + set.Id + "\""))) ||
            ((purpose == null || purpose == "" || purpose == "exam") && year == null && !db.TaskHistories.Any(history =>
                history.TaskId == set.TaskId && history.Content.StartsWith(TaskJournalCodec.Prefix) &&
                history.Content.Contains("\"stream\":\"exam:" + set.Id + "\""))));
    }
}
