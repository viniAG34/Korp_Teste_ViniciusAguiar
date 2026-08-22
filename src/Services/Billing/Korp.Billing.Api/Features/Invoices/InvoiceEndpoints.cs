using Korp.Billing.Api.Correlation;
using Korp.Billing.Api.Features.Invoices.Contracts;
using Korp.Billing.Api.Security;
using Korp.Billing.Application.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace Korp.Billing.Api.Features.Invoices;

public static class InvoiceEndpoints
{
    private static readonly Action<ILogger, Guid, long, Guid, Guid, Exception?> LogInvoiceCreated =
        LoggerMessage.Define<Guid, long, Guid, Guid>(LogLevel.Information, new EventId(3001, "InvoiceCreated"),
            "Invoice created: {InvoiceId} {InvoiceNumber} by {CreatedByUserId}; correlation {CorrelationId}");
    private static readonly Action<ILogger, string, Guid, Guid, Guid, Exception?> LogItemChanged =
        LoggerMessage.Define<string, Guid, Guid, Guid>(LogLevel.Information, new EventId(3002, "InvoiceItemChanged"),
            "Invoice item operation {Operation}: invoice {InvoiceId}, item {InvoiceItemId}, correlation {CorrelationId}");

    public static IEndpointRouteBuilder MapInvoiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/invoices", CreateAsync).WithName("CreateInvoice").WithTags("Invoices")
            .RequireAuthorization(AuthenticationExtensions.AdminOnlyPolicy).Produces<InvoiceResponse>(201);
        endpoints.MapGet("/api/v1/invoices", ListAsync).WithName("ListInvoices").WithTags("Invoices")
            .RequireAuthorization(AuthenticationExtensions.AuthenticatedUserPolicy).Produces<InvoicePageResponse>();
        endpoints.MapGet("/api/v1/invoices/{invoiceId}", GetByIdAsync).WithName("GetInvoiceById").WithTags("Invoices")
            .RequireAuthorization(AuthenticationExtensions.AuthenticatedUserPolicy).Produces<InvoiceResponse>();
        endpoints.MapPost("/api/v1/invoices/{invoiceId}/items", AddItemAsync).WithName("AddInvoiceItem").WithTags("Invoice Items")
            .RequireAuthorization(AuthenticationExtensions.AdminOnlyPolicy).Accepts<AddInvoiceItemRequest>("application/json").Produces<InvoiceResponse>();
        endpoints.MapPut("/api/v1/invoices/{invoiceId}/items/{itemId}", UpdateItemAsync).WithName("UpdateInvoiceItemQuantity").WithTags("Invoice Items")
            .RequireAuthorization(AuthenticationExtensions.AdminOnlyPolicy).Accepts<UpdateInvoiceItemRequest>("application/json").Produces<InvoiceResponse>();
        endpoints.MapDelete("/api/v1/invoices/{invoiceId}/items/{itemId}", RemoveItemAsync).WithName("RemoveInvoiceItem").WithTags("Invoice Items")
            .RequireAuthorization(AuthenticationExtensions.AdminOnlyPolicy).Produces(204);
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateInvoiceHandler handler, HttpContext context, CancellationToken token)
    {
        if (!AuthenticationExtensions.TryGetUserId(context.User, out var userId)) return Unauthorized(context);
        var invoice = await handler.HandleAsync(new CreateInvoiceCommand(userId), token);
        InvoiceEndpointResults.SetEtag(context, invoice.Version);
        LogInvoiceCreated(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("InvoiceCreated"),
            invoice.Id, invoice.Number, userId, CorrelationMiddleware.Get(context), null);
        return Results.Created($"/api/v1/invoices/{invoice.Id:D}", InvoiceResponseMapper.Map(invoice));
    }

    private static async Task<IResult> GetByIdAsync(string invoiceId, GetInvoiceByIdHandler handler, HttpContext context, CancellationToken token)
    {
        if (!InvoiceEndpointResults.TryInvoiceId(invoiceId, context, out var id, out var error)) return error!;
        var invoice = await handler.HandleAsync(new GetInvoiceByIdQuery(id), token);
        if (invoice is null) return InvoiceEndpointResults.Problem(context, 404, "invoice_not_found", "Nota não encontrada", "A nota informada não foi encontrada.");
        InvoiceEndpointResults.SetEtag(context, invoice.Version);
        return Results.Ok(InvoiceResponseMapper.Map(invoice));
    }

    private static async Task<IResult> ListAsync(int? pageNumber, int? pageSize, ListInvoicesHandler handler, HttpContext context, CancellationToken token)
    {
        var page = pageNumber ?? 1; var size = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (page < 1) errors["pageNumber"] = ["A página deve ser maior ou igual a 1."];
        if (size is < 1 or > 100) errors["pageSize"] = ["O tamanho da página deve estar entre 1 e 100."];
        if (errors.Count > 0) return Validation(context, errors);
        var result = await handler.HandleAsync(new ListInvoicesQuery(page, size), token);
        return Results.Ok(new InvoicePageResponse(result.Items.Select(InvoiceResponseMapper.Map).ToArray(),
            result.PageNumber, result.PageSize, result.TotalCount, result.TotalPages));
    }

    private static async Task<IResult> AddItemAsync(string invoiceId, AddInvoiceItemRequest request,
        AddInvoiceItemHandler handler, HttpContext context, CancellationToken token)
    {
        if (!InvoiceEndpointResults.TryInvoiceId(invoiceId, context, out var id, out var error)) return error!;
        if (!InvoiceEndpointResults.TryExpectedVersion(context, out var version, out error)) return error!;
        var errors = ValidateItem(request.ProductId, request.Quantity);
        if (errors.Count > 0) return Validation(context, errors);
        var result = await handler.HandleAsync(new AddInvoiceItemCommand(id, request.ProductId, request.Quantity, version), token);
        return MutationResponse(context, result, "add",
            result.Invoice?.Items.Single(item => item.ProductId == request.ProductId).Id ?? Guid.Empty);
    }

    private static async Task<IResult> UpdateItemAsync(string invoiceId, string itemId, UpdateInvoiceItemRequest request,
        UpdateInvoiceItemQuantityHandler handler, HttpContext context, CancellationToken token)
    {
        if (!InvoiceEndpointResults.TryInvoiceId(invoiceId, context, out var id, out var error)) return error!;
        if (!InvoiceEndpointResults.TryItemId(itemId, context, out var parsedItemId, out error)) return error!;
        if (!InvoiceEndpointResults.TryExpectedVersion(context, out var version, out error)) return error!;
        if (request.Quantity <= 0) return Validation(context, new Dictionary<string, string[]> { ["quantity"] = ["A quantidade deve ser maior que zero."] });
        var result = await handler.HandleAsync(new UpdateInvoiceItemQuantityCommand(id, parsedItemId, request.Quantity, version), token);
        return MutationResponse(context, result, "update", parsedItemId);
    }

    private static async Task<IResult> RemoveItemAsync(string invoiceId, string itemId,
        RemoveInvoiceItemHandler handler, HttpContext context, CancellationToken token)
    {
        if (!InvoiceEndpointResults.TryInvoiceId(invoiceId, context, out var id, out var error)) return error!;
        if (!InvoiceEndpointResults.TryItemId(itemId, context, out var parsedItemId, out error)) return error!;
        if (!InvoiceEndpointResults.TryExpectedVersion(context, out var version, out error)) return error!;
        var result = await handler.HandleAsync(new RemoveInvoiceItemCommand(id, parsedItemId, version), token);
        if (result.Status != InvoiceMutationStatus.Success) return InvoiceEndpointResults.MutationFailure(context, result.Status);
        InvoiceEndpointResults.SetEtag(context, result.Invoice!.Version);
        LogItemChanged(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("InvoiceItemChanged"),
            "remove", result.Invoice.Id, parsedItemId, CorrelationMiddleware.Get(context), null);
        return Results.NoContent();
    }

    private static IResult MutationResponse(
        HttpContext context, InvoiceMutationResult result, string operation, Guid invoiceItemId)
    {
        if (result.Status != InvoiceMutationStatus.Success) return InvoiceEndpointResults.MutationFailure(context, result.Status);
        InvoiceEndpointResults.SetEtag(context, result.Invoice!.Version);
        LogItemChanged(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("InvoiceItemChanged"),
            operation, result.Invoice.Id, invoiceItemId, CorrelationMiddleware.Get(context), null);
        return Results.Ok(InvoiceResponseMapper.Map(result.Invoice));
    }

    private static Dictionary<string, string[]> ValidateItem(Guid productId, int quantity)
    {
        var errors = new Dictionary<string, string[]>();
        if (productId == Guid.Empty) errors["productId"] = ["Informe um produto válido."];
        if (quantity <= 0) errors["quantity"] = ["A quantidade deve ser maior que zero."];
        return errors;
    }

    private static IResult Validation(HttpContext context, IDictionary<string, string[]> errors) => Results.ValidationProblem(errors,
        type: "urn:korp:problem:validation-failed", title: "Dados inválidos", statusCode: 400,
        detail: "Revise os campos informados.", instance: context.Request.Path,
        extensions: new Dictionary<string, object?> { ["code"] = "validation_failed", ["traceId"] = context.TraceIdentifier });
    private static IResult Unauthorized(HttpContext context) => InvoiceEndpointResults.Problem(context, 401,
        "authentication_required", "Autenticação necessária", "A identidade autenticada não contém autoria válida.");
}
