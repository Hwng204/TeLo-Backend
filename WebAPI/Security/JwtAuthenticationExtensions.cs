using System.Security.Claims;
using System.Text;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace WebAPI.Security;

public static class JwtAuthenticationExtensions
{
    // Login (POST /api/auth/login) and Bearer access-token validation. Needs Jwt:SigningKey (32+ chars).
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "LeTo-Backend";
        var audience = configuration["Jwt:Audience"] ?? "LeTo-Frontend";
        var signingKey = configuration["Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 characters.");
        }

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddUserAuthentication();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        if (!ulong.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                        {
                            context.Fail("Invalid user identity.");
                            return;
                        }
                        // Tokens issued before this migration have version 1. Any account change
                        // increments the persisted version, revoking those legacy tokens as well.
                        var value = principal.FindFirstValue("security_version") ?? "1";
                        if (!uint.TryParse(value, out var version))
                        {
                            context.Fail("Invalid security version.");
                            return;
                        }
                        var authentication = context.HttpContext.RequestServices.GetRequiredService<IUserAuthenticationService>();
                        if (!await authentication.IsSessionValidAsync(userId, version, context.HttpContext.RequestAborted))
                            context.Fail("Account disabled or token revoked.");
                    }
                };
            });
        services.AddAuthorization();
        return services;
    }
}
