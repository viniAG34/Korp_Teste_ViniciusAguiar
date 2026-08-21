using Korp.Billing.Api.Errors;
using Korp.Billing.Api.Http;
using Korp.Billing.Application.Invoices;

namespace Korp.Billing.Api.Features.Invoices;

internal static class InvoiceEndpointResults
{
    public static bool TryInvoiceId(string value, HttpContext context, out Guid id, out IResult? error)
    {
        if (Guid.TryParse(value, out id) && id != Guid.Empty) { error = null; return true; }
        error = Problem(context, 400, "invalid_invoice_id", "Identificador inválido", "Informe um identificador de nota válido.");
        return false;
    }

    public static bool TryItemId(string value, HttpContext context, out Guid id, out IResult? error)
    {
        if (Guid.TryParse(value, out id) && id != Guid.Empty) { error = null; return true; }
        error = Problem(context, 400, "invalid_invoice_item_id", "Identificador inválido", "Informe um identificador de item válido.");
        return false;
    }

    public static bool TryExpectedVersion(HttpContext context, out uint version, out IResult? error)
    {
        var parsed = EntityTag.Parse(context.Request.Headers.IfMatch.ToString());
        version = parsed.Value?.Version ?? 0;
        error = parsed.Status switch
        {
            EntityTagParseStatus.Missing => Problem(context, 428, "invoice_version_required", "Versão necessária", "Informe o header If-Match."),
            EntityTagParseStatus.Invalid => Problem(context, 400, "invalid_if_match", "Versão inválida", "Informe um ETag forte e válido."),
            _ => null
        };
        return error is null;
    }

    public static IResult MutationFailure(HttpContext context, InvoiceMutationStatus status) => status switch
    {
        InvoiceMutationStatus.InvoiceNotFound => Problem(context, 404, "invoice_not_found", "Nota não encontrada", "A nota informada não foi encontrada."),
        InvoiceMutationStatus.ItemNotFound => Problem(context, 404, "invoice_item_not_found", "Item não encontrado", "O item informado não pertence à nota."),
        InvoiceMutationStatus.ProductNotFound => Problem(context, 404, "product_not_found", "Produto não encontrado", "O produto informado não foi encontrado."),
        InvoiceMutationStatus.ProductAlreadyAdded => Problem(context, 409, "product_already_added", "Produto já incluído", "O produto já está presente na nota."),
        InvoiceMutationStatus.InvoiceNotOpen => Problem(context, 409, "invoice_not_open", "Nota não está aberta", "A nota não permite esta operação."),
        InvoiceMutationStatus.IssuanceInProgress => Problem(context, 409, "invoice_issuance_in_progress", "Emissão em andamento", "A nota está bloqueada por uma emissão."),
        InvoiceMutationStatus.VersionMismatch => Problem(context, 412, "invoice_version_mismatch", "Versão desatualizada", "A nota foi alterada por outra operação."),
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static IResult Problem(HttpContext context, int status, string code, string title, string detail) =>
        Results.Problem(ApiProblemDetails.Create(context, status, code, title, detail));

    public static void SetEtag(HttpContext context, uint version) =>
        context.Response.Headers.ETag = new EntityTag(version).ToHeaderValue();
}
