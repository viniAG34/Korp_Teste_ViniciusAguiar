namespace Korp.Identity.Application.Authentication;

public sealed record AccessToken(string Value, int ExpiresInSeconds, DateTimeOffset ExpiresAtUtc);
