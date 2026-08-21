using Korp.Identity.Application.Authentication;
using Microsoft.AspNetCore.Diagnostics;

namespace Korp.Identity.Api.Errors;

public sealed class IdentityExceptionHandler(
    ILogger<IdentityExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, Exception?> LogUnavailable = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1001, "IdentityUnavailable"),
        "Identity operation failed because its dependency is unavailable");

    private static readonly Action<ILogger, Exception?> LogUnexpected = LoggerMessage.Define(
        LogLevel.Error,
        new EventId(1002, "UnexpectedIdentityFailure"),
        "Unexpected identity operation failure");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var unavailable = exception is IdentityServiceUnavailableException;
        var status = unavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
        var code = unavailable ? "identity_unavailable" : "unexpected_error";
        var detail = unavailable
            ? "O serviço de identidade está temporariamente indisponível."
            : "Não foi possível concluir a solicitação.";

        if (unavailable)
        {
            LogUnavailable(logger, null);
        }
        else
        {
            LogUnexpected(logger, exception);
        }

        var problem = ApiProblemDetails.Create(
            httpContext,
            status,
            code,
            unavailable ? "Serviço indisponível" : "Erro inesperado",
            detail);
        await Results.Problem(problem).ExecuteAsync(httpContext);
        return true;
    }
}
