namespace Korp.Inventory.Api.Security;

public sealed class JwtValidationOptions
{
    public const string SectionName = "Jwt";
    public string SigningKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
}
