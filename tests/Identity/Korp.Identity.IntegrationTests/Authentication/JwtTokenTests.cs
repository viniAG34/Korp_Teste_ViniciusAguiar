using Korp.Identity.Application.Authentication;
using Korp.Identity.Infrastructure.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Korp.Identity.IntegrationTests.Authentication;

public sealed class JwtTokenTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 8, 20, 15, 0, 0, TimeSpan.Zero);
    private static readonly byte[] SigningKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public void IssuerCreatesHs256TokenWithOnlyApprovedIdentityClaims()
    {
        var options = ValidOptions();
        var issuer = new JwtTokenIssuer(Options.Create(options), new FixedTimeProvider(FixedNow));
        var userId = Guid.NewGuid();

        var result = issuer.Issue(new AuthenticatedIdentity(
            userId,
            "admin@example.com",
            ["Viewer", "Admin"]));
        var token = new JsonWebTokenHandler().ReadJsonWebToken(result.Value);

        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Alg);
        Assert.Equal(options.Issuer, token.Issuer);
        Assert.Contains(options.Audience, token.Audiences);
        Assert.Equal(userId.ToString("D"), token.Claims.Single(claim => claim.Type == JwtClaimNames.Subject).Value);
        Assert.Equal("admin@example.com", token.Claims.Single(claim => claim.Type == JwtClaimNames.Email).Value);
        Assert.Equal(["Admin", "Viewer"], token.Claims.Where(claim => claim.Type == JwtClaimNames.Role).Select(claim => claim.Value));
        Assert.True(Guid.TryParse(token.Claims.Single(claim => claim.Type == JwtClaimNames.JwtId).Value, out _));
        Assert.Equal(900, result.ExpiresInSeconds);
        Assert.Equal(FixedNow.AddMinutes(15), result.ExpiresAtUtc);
    }

    [Fact]
    public async Task EmittedTokenPassesStrictValidation()
    {
        var options = ValidOptions();
        var issuer = new JwtTokenIssuer(Options.Create(options), new FixedTimeProvider(FixedNow));
        var token = issuer.Issue(new AuthenticatedIdentity(
            Guid.NewGuid(),
            "admin@example.com",
            ["Admin"]));
        var parameters = ValidationParameters(options, FixedNow.AddMinutes(1));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, parameters);

        Assert.True(result.IsValid, result.Exception?.Message);
    }

    [Fact]
    public async Task WrongIssuerIsRejected()
    {
        var options = ValidOptions();
        var issuer = new JwtTokenIssuer(Options.Create(options), new FixedTimeProvider(FixedNow));
        var token = issuer.Issue(new AuthenticatedIdentity(Guid.NewGuid(), "admin@example.com", ["Admin"]));
        var parameters = ValidationParameters(options, FixedNow.AddMinutes(1));
        parameters.ValidIssuer = "other-issuer";

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, parameters);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("", "issuer", "audience")]
    [InlineData("not-base64", "issuer", "audience")]
    [InlineData("AQID", "issuer", "audience")]
    [InlineData("AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=", "", "audience")]
    [InlineData("AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=", "issuer", "")]
    public void ValidatorRejectsUnsafeConfiguration(string key, string issuer, string audience)
    {
        var result = new JwtOptionsValidator().Validate(null, new JwtOptions
        {
            SigningKey = key,
            Issuer = issuer,
            Audience = audience
        });

        Assert.False(result.Succeeded);
    }

    private static JwtOptions ValidOptions() => new()
    {
        SigningKey = Convert.ToBase64String(SigningKey),
        Issuer = "korp-identity",
        Audience = "korp-erp-api"
    };

    private static TokenValidationParameters ValidationParameters(JwtOptions options, DateTimeOffset now) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(SigningKey),
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateLifetime = true,
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        LifetimeValidator = (notBefore, expires, _, _) =>
            notBefore <= now.UtcDateTime && expires >= now.UtcDateTime
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
