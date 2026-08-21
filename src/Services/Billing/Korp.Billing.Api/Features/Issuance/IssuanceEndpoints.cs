using Korp.Billing.Api.Correlation;
using Korp.Billing.Api.Features.Invoices;
using Korp.Billing.Api.Features.Issuance.Contracts;
using Korp.Billing.Api.Http;
using Korp.Billing.Api.Security;
using Korp.Billing.Application.Issuance;

namespace Korp.Billing.Api.Features.Issuance;

public static class IssuanceEndpoints
{
    public static IEndpointRouteBuilder MapIssuanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/invoices/{invoiceId}/print", PrintAsync).WithName("PrintInvoice").WithTags("Issuance")
            .RequireAuthorization(AuthenticationExtensions.AdminOnlyPolicy).Produces<InvoiceIssuanceProcessResponse>(202);
        endpoints.MapGet("/api/v1/invoice-issuance-processes/{processId}", GetAsync).WithName("GetInvoiceIssuanceProcess").WithTags("Issuance")
            .RequireAuthorization(AuthenticationExtensions.AuthenticatedUserPolicy).Produces<InvoiceIssuanceProcessResponse>();
        return endpoints;
    }

    private static async Task<IResult> PrintAsync(string invoiceId, PrintInvoiceHandler handler, HttpContext context, CancellationToken token)
    {
        if (!InvoiceEndpointResults.TryInvoiceId(invoiceId, context, out var id, out var error)) return error!;
        if (!InvoiceEndpointResults.TryExpectedVersion(context, out var version, out error)) return error!;
        var key = IdempotencyKey.Parse(context.Request.Headers["Idempotency-Key"].ToString());
        if (key.Status == IdempotencyKeyParseStatus.Missing)
            return InvoiceEndpointResults.Problem(context, 400, "idempotency_key_required", "Chave necessária", "Informe o header Idempotency-Key.");
        if (key.Status == IdempotencyKeyParseStatus.Invalid)
            return InvoiceEndpointResults.Problem(context, 400, "invalid_idempotency_key", "Chave inválida", "Informe Idempotency-Key como UUID canônico.");
        if (!AuthenticationExtensions.TryGetUserId(context.User, out var userId))
            return InvoiceEndpointResults.Problem(context, 401, "authentication_required", "Autenticação necessária", "A identidade não contém autoria válida.");

        var result = await handler.HandleAsync(new PrintInvoiceCommand(id, key.Value!.Value.Value, version,
            userId, CorrelationMiddleware.Get(context)), token);
        if (result.Process is null) return PrintFailure(context, result.Status);
        var process = result.Process;
        var location = $"/api/v1/invoice-issuance-processes/{process.Id:D}";
        context.Response.Headers.Location = location;
        InvoiceEndpointResults.SetEtag(context, process.InvoiceVersion);
        if (process.RetryAfterSeconds is { } retry) context.Response.Headers.RetryAfter = retry.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var response = InvoiceResponseMapper.Map(process);
        return result.Status is PrintInvoiceStatus.Accepted or PrintInvoiceStatus.ReplayedActive
            ? Results.Json(response, statusCode: 202)
            : Results.Ok(response);
    }

    private static async Task<IResult> GetAsync(string processId, GetIssuanceProcessHandler handler, HttpContext context, CancellationToken token)
    {
        if (!Guid.TryParse(processId, out var id) || id == Guid.Empty)
            return InvoiceEndpointResults.Problem(context, 400, "invalid_issuance_process_id", "Identificador inválido", "Informe um identificador de processo válido.");
        var process = await handler.HandleAsync(new GetIssuanceProcessQuery(id), token);
        if (process is null) return InvoiceEndpointResults.Problem(context, 404, "issuance_process_not_found", "Processo não encontrado", "O processo informado não foi encontrado.");
        if (process.RetryAfterSeconds is { } retry) context.Response.Headers.RetryAfter = retry.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Ok(InvoiceResponseMapper.Map(process));
    }

    private static IResult PrintFailure(HttpContext context, PrintInvoiceStatus status) => status switch
    {
        PrintInvoiceStatus.InvoiceNotFound => InvoiceEndpointResults.Problem(context, 404, "invoice_not_found", "Nota não encontrada", "A nota informada não foi encontrada."),
        PrintInvoiceStatus.VersionMismatch => InvoiceEndpointResults.Problem(context, 412, "invoice_version_mismatch", "Versão desatualizada", "A nota foi alterada por outra operação."),
        PrintInvoiceStatus.InvoiceNotOpen => InvoiceEndpointResults.Problem(context, 409, "invoice_not_open", "Nota não está aberta", "Somente notas abertas podem iniciar emissão."),
        PrintInvoiceStatus.InvoiceEmpty => InvoiceEndpointResults.Problem(context, 409, "invoice_empty", "Nota vazia", "Inclua ao menos um item antes da emissão."),
        PrintInvoiceStatus.IssuanceInProgress => InvoiceEndpointResults.Problem(context, 409, "invoice_issuance_in_progress", "Emissão em andamento", "A nota já possui emissão ativa."),
        PrintInvoiceStatus.IdempotencyKeyReused => InvoiceEndpointResults.Problem(context, 409, "idempotency_key_reused", "Chave já utilizada", "A chave idempotente já foi utilizada."),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
}
