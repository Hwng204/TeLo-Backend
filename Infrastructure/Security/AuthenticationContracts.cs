namespace Infrastructure.Security;

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthenticatedUser(
    ulong UserId,
    string Username,
    IReadOnlyList<string> RoleCodes,
    ulong? BranchId = null,
    uint SecurityVersion = 1);

public interface IUserAuthenticationService
{
    Task<bool> IsSessionValidAsync(ulong userId, uint securityVersion, CancellationToken cancellationToken);

    Task<AuthenticatedUser?> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken);
}
