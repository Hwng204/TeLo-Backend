namespace Application.Common;

public static class ExamStatusCodes
{
    public const string Draft = "DRAFT";
    public const string Scheduled = "SCHEDULED";
    public const string Ongoing = "ONGOING";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Draft, Scheduled, Ongoing, Completed, Cancelled],
        StringComparer.OrdinalIgnoreCase);
}
