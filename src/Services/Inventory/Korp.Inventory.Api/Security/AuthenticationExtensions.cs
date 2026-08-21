using System.Security.Claims;
using Korp.Inventory.Api.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Korp.Inventory.Api.Security;

public static class AuthenticationExtensions
{
    public const string AuthenticatedUserPolicy = "AuthenticatedUser";
    public const string AdminOnlyPolicy = "AdminOnly";

    public static IServiceCollection AddInventorySecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtValidationOptions.SectionName)
            .Get<JwtValidationOptions>() ?? new JwtValidationOptions();
        var key = DecodeKey(options.SigningKey);
        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("JWT validation configuration is invalid.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "email",
                    RoleClaimType = "role"
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal!;
                        var sub = principal.FindFirstValue("sub");
                        if (!Guid.TryParse(sub, out _)
                            || string.IsNullOrWhiteSpace(principal.FindFirstValue("email"))
                            || !principal.FindAll("role").Any(claim => !string.IsNullOrWhiteSpace(claim.Value)))
                        {
                            context.Fail("Required identity claims are missing.");
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteSecurityProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "authentication_required",
                            "Autenticação necessária",
                            "Informe um token de acesso válido.");
                    },
                    OnForbidden = context => WriteSecurityProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        "access_denied",
                        "Acesso negado",
                        "A identidade não possui permissão para esta operação.")
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthenticatedUserPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("sub")
                .RequireClaim("email"))
            .AddPolicy(AdminOnlyPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("sub")
                .RequireClaim("email")
                .RequireRole("Admin"));
        return services;
    }

    public static bool TryGetUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue("sub"), out userId) && userId != Guid.Empty;

    private static byte[] DecodeKey(string value)
    {
        try
        {
            var key = Convert.FromBase64String(value);
            if (key.Length >= 32)
            {
                return key;
            }
        }
        catch (FormatException)
        {
        }

        throw new InvalidOperationException("JWT validation configuration is invalid.");
    }

    private static async Task WriteSecurityProblemAsync(
        HttpContext context,
        int status,
        string code,
        string title,
        string detail)
    {
        await Results.Problem(ApiProblemDetails.Create(context, status, code, title, detail))
            .ExecuteAsync(context);
    }
}
