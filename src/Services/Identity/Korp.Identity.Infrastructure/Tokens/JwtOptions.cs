namespace Korp.Identity.Infrastructure.Tokens;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int AccessTokenLifetimeSeconds = 900;

    public string SigningKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
}
