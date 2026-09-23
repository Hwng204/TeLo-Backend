using Infrastructure.Security;

namespace Infrastructure.Tests.Persistence;

internal sealed class TestRoleCatalog : IMatrixRoleCatalog
{
    public IReadOnlyCollection<string> PhtRoleCodes { get; } = ["PHT"];
    public IReadOnlyCollection<string> TeamLeadRoleCodes { get; } = ["TEAM_LEAD"];
    public IReadOnlyCollection<string> PrincipalRoleCodes { get; } = ["HIEU_TRUONG"];

    public bool IsPht(string roleCode) =>
        PhtRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);

    public bool IsTeamLead(string roleCode) =>
        TeamLeadRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);

    public bool IsPrincipal(string roleCode) =>
        PrincipalRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
}
