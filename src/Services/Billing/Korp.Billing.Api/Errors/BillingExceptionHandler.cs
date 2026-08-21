using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace Korp.Billing.Api.Errors;

public sealed class BillingExceptionHandler(ILogger<BillingExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, string, Exception?> LogFailure = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(3101, "BillingFailure"), "Billing request failed with code {ErrorCode}");

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title, detail) = exception switch
        {
            ProductCatalogUnavailableException => (503, "product_catalog_unavailable", "Catálogo indisponível", "O catálogo de produtos está temporariamente indisponível."),
            BillingServiceUnavailableException => (503, "billing_unavailable", "Serviço indisponível", "O serviço de faturamento está temporariamente indisponível."),
            DomainRuleException => (400, "validation_failed", "Dados inválidos", "Revise os dados informados."),
            _ => (500, "unexpected_error", "Erro inesperado", "Não foi possível concluir a solicitação.")
        };
        if (status >= 500) LogFailure(logger, code, exception);
        await Results.Problem(ApiProblemDetails.Create(httpContext, status, code, title, detail))
            .ExecuteAsync(httpContext);
        return true;
    }
}
