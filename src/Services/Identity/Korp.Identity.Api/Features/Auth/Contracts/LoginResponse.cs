namespace Korp.Identity.Api.Features.Auth.Contracts;

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    DateTimeOffset ExpiresAtUtc,
    AuthenticatedUserResponse User);
