namespace WebAPI.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "LeTo-Backend";
    public string Audience { get; set; } = "LeTo-Frontend";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenMinutes { get; set; } = 10080; // 7 days
}
