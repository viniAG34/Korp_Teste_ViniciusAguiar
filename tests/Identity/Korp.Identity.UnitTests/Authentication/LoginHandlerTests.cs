using Korp.Identity.Application.Authentication;

namespace Korp.Identity.UnitTests.Authentication;

public sealed class LoginHandlerTests
{
    [Fact]
    public async Task InvalidCredentialsDoNotIssueToken()
    {
        var authenticator = new StubAuthenticator(CredentialAuthenticationResult.Invalid());
        var issuer = new RecordingTokenIssuer();
        var handler = new LoginHandler(authenticator, issuer);

        var result = await handler.HandleAsync(
            new LoginCommand(" admin@example.com ", "wrong"),
            TestContext.Current.CancellationToken);

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Identity);
        Assert.Null(result.AccessToken);
        Assert.Equal(0, issuer.CallCount);
        Assert.Equal("admin@example.com", authenticator.ReceivedEmail);
        Assert.Equal("wrong", authenticator.ReceivedPassword);
    }

    [Fact]
    public async Task ValidCredentialsIssueTokenAfterAuthentication()
    {
        var identity = new AuthenticatedIdentity(Guid.NewGuid(), "admin@example.com", ["Admin"]);
        var authenticator = new StubAuthenticator(CredentialAuthenticationResult.Success(identity));
        var issuer = new RecordingTokenIssuer();
        var handler = new LoginHandler(authenticator, issuer);

        var result = await handler.HandleAsync(
            new LoginCommand(identity.Email, "Strong-Password-2026!"),
            TestContext.Current.CancellationToken);

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.Same(identity, result.Identity);
        Assert.Equal("token", result.AccessToken?.Value);
        Assert.Equal(1, issuer.CallCount);
    }

    [Fact]
    public async Task CancellationIsPropagatedBeforeAuthentication()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = new LoginHandler(
            new StubAuthenticator(CredentialAuthenticationResult.Invalid()),
            new RecordingTokenIssuer());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new LoginCommand("admin@example.com", "password"), cancellation.Token));
    }

    private sealed class StubAuthenticator(CredentialAuthenticationResult result) : ICredentialAuthenticator
    {
        public string? ReceivedEmail { get; private set; }
        public string? ReceivedPassword { get; private set; }

        public Task<CredentialAuthenticationResult> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            ReceivedEmail = email;
            ReceivedPassword = password;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingTokenIssuer : IAccessTokenIssuer
    {
        public int CallCount { get; private set; }

        public AccessToken Issue(AuthenticatedIdentity identity)
        {
            CallCount++;
            return new AccessToken("token", 900, DateTimeOffset.UtcNow.AddMinutes(15));
        }
    }
}
