using Domain.Entities.Identity;

namespace Infrastructure.Repositories.Implement;

public static class RoleAssignmentQuery
{
    // Use the same effective-role boundary for database authorization and reference lists.
    // Retained grants on inactive roles are history, not current privileges. Roles are global (no unit scope).
    public static IQueryable<UserRole> WhereEffective(this IQueryable<UserRole> assignments) =>
        assignments.Where(assignment => assignment.Role.Status == "ACTIVE");
}
