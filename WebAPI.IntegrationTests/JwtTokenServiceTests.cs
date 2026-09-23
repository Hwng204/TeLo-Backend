using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Infrastructure.Security;
using Microsoft.Extensions.Options;
using WebAPI.Security;

namespace WebAPI.IntegrationTests;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void TokenContainsUserAndRoleClaims()
    {
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "integration-test-signing-key-with-at-least-32-chars",
            AccessTokenMinutes = 10
        }));

        var response = service.CreateToken(new AuthenticatedUser(42, "matrix-user", ["PHT"]));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        Assert.Equal("42", token.Claims.Single(claim => claim.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("PHT", token.Claims.Single(claim => claim.Type == "role").Value);
        Assert.Equal("test-issuer", token.Issuer);
        Assert.Contains("test-audience", token.Audiences);
    }
}
