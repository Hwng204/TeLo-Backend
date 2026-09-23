using Application.Services.Implement;
using Infrastructure.Security;
using Infrastructure.UnitOfWork;

namespace Application.Tests.Fakes;

/// <summary>Builds the real people resolver over a fake unit of work, with the default role codes.</summary>
internal static class TestPeople
{
    public static MatrixPeopleResolver Resolver(IUnitOfWork uow) => new(uow, new DefaultRoleCatalog());

    private sealed class DefaultRoleCatalog : IMatrixRoleCatalog
    {
        public IReadOnlyCollection<string> PhtRoleCodes { get; } = new[] { "PHT" };
        public IReadOnlyCollection<string> TeamLeadRoleCodes { get; } = new[] { "TEAM_LEAD", "TO_TRUONG" };
        public IReadOnlyCollection<string> PrincipalRoleCodes { get; } = new[] { "HIEU_TRUONG", "PRINCIPAL" };

        public bool IsPht(string roleCode) => PhtRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
        public bool IsPrincipal(string roleCode) => PrincipalRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
        public bool IsTeamLead(string roleCode) => TeamLeadRoleCodes.Contains(roleCode, StringComparer.OrdinalIgnoreCase);
    }
}
