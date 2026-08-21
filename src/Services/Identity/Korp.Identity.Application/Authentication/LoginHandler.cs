namespace Korp.Identity.Application.Authentication;

public sealed class LoginHandler(
    ICredentialAuthenticator credentialAuthenticator,
    IAccessTokenIssuer accessTokenIssuer)
{
    public async Task<LoginResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var authentication = await credentialAuthenticator.AuthenticateAsync(
            command.Email.Trim(),
            command.Password,
            cancellationToken);

        if (!authentication.IsAuthenticated || authentication.Identity is null)
        {
            return LoginResult.InvalidCredentials();
        }

        var accessToken = accessTokenIssuer.Issue(authentication.Identity);
        return LoginResult.Success(authentication.Identity, accessToken);
    }
}
