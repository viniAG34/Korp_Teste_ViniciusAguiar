using Korp.Identity.Api.Errors;
using Korp.Identity.Api.Features.Auth.Contracts;
using Korp.Identity.Application.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Korp.Identity.Api.Features.Auth;

public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLoginEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/auth/login", HandleAsync)
            .WithName("Login")
            .WithTags("Auth")
            .AllowAnonymous()
            .Accepts<LoginRequest>("application/json")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        LoginHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = LoginRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                type: "urn:korp:problem:validation-failed",
                title: "Dados inválidos",
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Revise os campos informados.",
                instance: httpContext.Request.Path,
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "validation_failed",
                    ["traceId"] = httpContext.TraceIdentifier
                });
        }

        var result = await handler.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            cancellationToken);
        if (result.Status == LoginStatus.InvalidCredentials)
        {
            return Results.Problem(ApiProblemDetails.Create(
                httpContext,
                StatusCodes.Status401Unauthorized,
                "invalid_credentials",
                "Credenciais inválidas",
                "E-mail ou senha inválidos."));
        }

        var identity = result.Identity
            ?? throw new InvalidOperationException("Successful login has no identity.");
        var token = result.AccessToken
            ?? throw new InvalidOperationException("Successful login has no access token.");
        return Results.Ok(new LoginResponse(
            token.Value,
            "Bearer",
            token.ExpiresInSeconds,
            token.ExpiresAtUtc,
            new AuthenticatedUserResponse(
                identity.UserId,
                identity.Email,
                identity.Roles)));
    }
}
