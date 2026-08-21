namespace Korp.Identity.Application.Authentication;

public sealed record LoginResult(
    LoginStatus Status,
    AuthenticatedIdentity? Identity,
    AccessToken? AccessToken)
{
    public static LoginResult InvalidCredentials() => new(LoginStatus.InvalidCredentials, null, null);

    public static LoginResult Success(AuthenticatedIdentity identity, AccessToken accessToken) =>
        new(LoginStatus.Success, identity, accessToken);
}
