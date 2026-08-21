namespace Korp.Identity.Application.Authentication;

public sealed record AuthenticatedIdentity(
    Guid UserId,
    string Email,
    IReadOnlyList<string> Roles);
