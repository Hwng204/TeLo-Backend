namespace Application.Common;

public static class ExamSubjectStatusCodes
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Active, Inactive],
        StringComparer.OrdinalIgnoreCase);
}
