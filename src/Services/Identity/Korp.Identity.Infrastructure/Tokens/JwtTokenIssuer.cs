using System.Globalization;
using System.Security.Claims;
using Korp.Identity.Application.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Korp.Identity.Infrastructure.Tokens;

public sealed class JwtTokenIssuer(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Issue(AuthenticatedIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (identity.UserId == Guid.Empty || string.IsNullOrWhiteSpace(identity.Email))
        {
            throw new ArgumentException("Authenticated identity is invalid.", nameof(identity));
        }

        if (!JwtOptionsValidator.TryDecodeSigningKey(_options.SigningKey, out var signingKey))
        {
            throw new InvalidOperationException("JWT signing configuration is invalid.");
        }

        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddSeconds(JwtOptions.AccessTokenLifetimeSeconds);
        var claims = new List<Claim>
        {
            new(JwtClaimNames.Subject, identity.UserId.ToString("D")),
            new(JwtClaimNames.Email, identity.Email),
            new(JwtClaimNames.JwtId, Guid.NewGuid().ToString("D")),
            new(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };
        claims.AddRange(identity.Roles
            .Order(StringComparer.Ordinal)
            .Select(role => new Claim(JwtClaimNames.Role, role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(signingKey),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, JwtOptions.AccessTokenLifetimeSeconds, expiresAt);
    }
}
