using Domain.Entities.Identity;
using Infrastructure.Context;
using Infrastructure.Repositories.Implement;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Security;

public sealed class UserAuthenticationService(
    ApplicationDbContext db,
    IPasswordHasher<User> passwordHasher) : IUserAuthenticationService
{
    // Operational admins without a teacher profile do not need an assigned branch.
    // A teacher cannot log in or keep using a token after their employing unit is disabled.
    private IQueryable<User> ActiveUsers() => db.Users.AsNoTracking().Where(user =>
        user.Status == "ACTIVE" && (!user.Teachers.Any() ||
            (user.SchoolBranch != null && user.SchoolBranch.Status == "ACTIVE" &&
             user.SchoolBranch.School.Status == "ACTIVE" &&
             !user.Teachers.Any(teacher => teacher.EmploymentStatus == "RESIGNED" || teacher.EmploymentStatus == "INACTIVE"))));

    public async Task<bool> IsSessionValidAsync(ulong userId, uint securityVersion, IReadOnlyCollection<string> roleCodes, CancellationToken cancellationToken)
    {
        if (!await ActiveUsers().AnyAsync(user => user.Id == userId && user.SecurityVersion == securityVersion, cancellationToken)) return false;
        // Scope can stop being effective when a school/branch is disabled, even if
        // that operation did not increment this non-teacher account's version.
        var effective = await db.UserRoles.AsNoTracking().WhereEffective().Where(grant => grant.UserId == userId)
            .Select(grant => grant.Role.Code.Trim()).ToArrayAsync(cancellationToken);
        return roleCodes.All(code => effective.Contains(code, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<AuthenticatedUser?> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var username = request.Username.Trim();
        var user = await ActiveUsers()
            .SingleOrDefaultAsync(
                item => item.Username == username,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        PasswordVerificationResult passwordResult;
        try
        {
            passwordResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        }
        catch (FormatException)
        {
            // Legacy/corrupt password data is not a valid credential and must not produce a 500.
            return null;
        }
        if (passwordResult is PasswordVerificationResult.Failed)
        {
            return null;
        }

        var effectiveCodes = await db.UserRoles.AsNoTracking().WhereEffective()
            .Where(grant => grant.UserId == user.Id)
            .Select(grant => grant.Role.Code.Trim()).ToArrayAsync(cancellationToken);
        var roleCodes = effectiveCodes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AuthenticatedUser(user.Id, user.Username, roleCodes, user.SchoolBranchId, user.SecurityVersion);
    }
}
