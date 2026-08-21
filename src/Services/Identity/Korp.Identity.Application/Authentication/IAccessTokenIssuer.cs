namespace Korp.Identity.Application.Authentication;

public interface IAccessTokenIssuer
{
    AccessToken Issue(AuthenticatedIdentity identity);
}
