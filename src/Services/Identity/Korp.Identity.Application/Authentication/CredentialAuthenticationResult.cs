namespace Korp.Identity.Application.Authentication;

public sealed record CredentialAuthenticationResult(
    bool IsAuthenticated,
    AuthenticatedIdentity? Identity)
{
    public static CredentialAuthenticationResult Invalid() => new(false, null);

    public static CredentialAuthenticationResult Success(AuthenticatedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return new CredentialAuthenticationResult(true, identity);
    }
}
