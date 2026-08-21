using System.Text.Json;
using Korp.Identity.Api.Features.Auth.Contracts;
using Korp.Identity.Api.Http;

namespace Korp.Identity.IntegrationTests.Contracts;

public sealed class IdentityHttpContractTests
{
    [Fact]
    public void LoginContractsExposeOnlyApprovedFields()
    {
        var requestProperties = typeof(LoginRequest).GetProperties().Select(property => property.Name).ToArray();
        var responseProperties = typeof(LoginResponse).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(["Email", "Password"], requestProperties);
        Assert.Equal(["AccessToken", "TokenType", "ExpiresInSeconds", "ExpiresAtUtc", "User"], responseProperties);
        Assert.DoesNotContain(responseProperties, property => property.Contains("Password", StringComparison.Ordinal));
        Assert.DoesNotContain(responseProperties, property => property.Contains("Refresh", StringComparison.Ordinal));
    }

    [Fact]
    public void LoginResponseUsesCamelCaseAndCanonicalUtcTimestamp()
    {
        var response = new LoginResponse(
            "token",
            "Bearer",
            900,
            DateTimeOffset.Parse("2026-08-18T15:15:00Z", System.Globalization.CultureInfo.InvariantCulture),
            new AuthenticatedUserResponse(Guid.NewGuid(), "admin@example.com", ["Admin"]));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, ApiJsonOptions.Create()));

        Assert.Equal("Bearer", json.RootElement.GetProperty("tokenType").GetString());
        Assert.Equal("2026-08-18T15:15:00+00:00", json.RootElement.GetProperty("expiresAtUtc").GetString());
        Assert.False(json.RootElement.TryGetProperty("refreshToken", out _));
    }
}
