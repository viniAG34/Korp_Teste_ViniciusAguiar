namespace Korp.Identity.Api.Features.Auth.Contracts;

public sealed record AuthenticatedUserResponse(Guid Id, string Email, IReadOnlyList<string> Roles);
