using Korp.Inventory.Application.Common;
using Korp.Inventory.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace Korp.Inventory.Api.Errors;

public sealed class InventoryExceptionHandler(ILogger<InventoryExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogWarning = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(2101, "InventoryUnavailable"),
        "Inventory request failed with code {ErrorCode}");
    private static readonly Action<ILogger, string, Exception?> LogInformation = LoggerMessage.Define<string>(
        LogLevel.Information, new EventId(2102, "InventoryRequestRejected"),
        "Inventory request failed with code {ErrorCode}");
    private static readonly Action<ILogger, string, Exception?> LogError = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(2103, "UnexpectedInventoryFailure"),
        "Inventory request failed with code {ErrorCode}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, title, detail, level) = exception switch
        {
            InventoryServiceUnavailableException => (
                503, "inventory_unavailable", "Serviço indisponível",
                "O serviço de estoque está temporariamente indisponível.", LogLevel.Warning),
            DomainRuleException => (
                400, "validation_failed", "Dados inválidos",
                "Revise os campos informados.", LogLevel.Information),
            _ => (
                500, "unexpected_error", "Erro inesperado",
                "Não foi possível concluir a solicitação.", LogLevel.Error)
        };
        if (level == LogLevel.Warning) LogWarning(logger, code, null);
        else if (level == LogLevel.Information) LogInformation(logger, code, null);
        else LogError(logger, code, exception);

        await Results.Problem(ApiProblemDetails.Create(httpContext, status, code, title, detail))
            .ExecuteAsync(httpContext);
        return true;
    }
}
