namespace Korp.Identity.Application.Authentication;

public interface ICredentialAuthenticator
{
    Task<CredentialAuthenticationResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken);
}
