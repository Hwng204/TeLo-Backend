using Application.DTOs;
using Application.Services.Interface;
using Infrastructure.Security;
using Infrastructure.UnitOfWork;

namespace Application.Services.Implement;

public sealed class MatrixPeopleResolver(
    IUnitOfWork uow,
    IMatrixRoleCatalog roleCatalog) : IMatrixPeopleResolver
{
    public const string PrincipalLabel = "Hiệu trưởng";
    public const string PhtLabel = "Phó Hiệu trưởng";
    public const string TeamLeadLabel = "Tổ trưởng";

    public async Task<IReadOnlyDictionary<ulong, MatrixPerson>> ResolveAsync(
        IEnumerable<ulong?> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<ulong, MatrixPerson>();
        }

        var rows = await uow.Matrices.GetPeopleAsync(ids, cancellationToken);
        return rows.ToDictionary(
            pair => pair.Key,
            pair => new MatrixPerson(pair.Key, pair.Value.FullName, RoleLabel(pair.Value.RoleCodes)));
    }

    /// <summary>
    /// A user may hold several roles; show the most senior one that matters to matrices
    /// (Principal, then PHT, then Team Lead). Anyone else has no label.
    /// </summary>
    private string? RoleLabel(IReadOnlyList<string> roleCodes)
    {
        if (roleCodes.Any(roleCatalog.IsPrincipal))
        {
            return PrincipalLabel;
        }

        if (roleCodes.Any(roleCatalog.IsPht))
        {
            return PhtLabel;
        }

        return roleCodes.Any(roleCatalog.IsTeamLead) ? TeamLeadLabel : null;
    }
}
