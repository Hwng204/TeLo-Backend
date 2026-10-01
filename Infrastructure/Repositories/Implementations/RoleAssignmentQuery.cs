using Domain.Entities.Identity;

namespace Infrastructure.Repositories.Implement;

public static class RoleAssignmentQuery
{
    // Use the same effective-role boundary for database authorization and reference lists.
    // Retained grants on inactive roles are history, not current privileges.
    public static IQueryable<UserRole> WhereEffective(this IQueryable<UserRole> assignments) => assignments.Where(assignment =>
        assignment.Role.Status == "ACTIVE" &&
        (!assignment.Role.SchoolId.HasValue ||
            (assignment.User.SchoolBranch != null &&
             assignment.User.SchoolBranch.SchoolId == assignment.Role.SchoolId &&
             assignment.User.SchoolBranch.Status == "ACTIVE" &&
             assignment.User.SchoolBranch.School.Status == "ACTIVE")) &&
        (!assignment.Role.SchoolBranchId.HasValue || assignment.User.SchoolBranchId == assignment.Role.SchoolBranchId));
}
