using Korp.Inventory.Api.Errors;
using Korp.Inventory.Api.Features.Products.Contracts;
using Korp.Inventory.Api.Observability;
using Korp.Inventory.Api.Security;
using Korp.Inventory.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace Korp.Inventory.Api.Features.Products;

public static class ProductEndpoints
{
    private static readonly Action<ILogger, Guid, string, Guid, Exception?> LogCreated =
        LoggerMessage.Define<Guid, string, Guid>(
            LogLevel.Information,
            new EventId(2001, "ProductCreated"),
            "Product created: {ProductId} {ProductCode} by {CreatedByUserId}");

    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/products", CreateAsync)
            .WithName("CreateProduct").WithTags("Products")
            .RequireAuthorization(AuthenticationExtensions.AdminOnlyPolicy)
            .Accepts<CreateProductRequest>("application/json")
            .Produces<ProductResponse>(StatusCodes.Status201Created)
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json");

        endpoints.MapGet("/api/v1/products", ListAsync)
            .WithName("ListProducts").WithTags("Products")
            .RequireAuthorization(AuthenticationExtensions.AuthenticatedUserPolicy)
            .Produces<ProductPageResponse>()
            .Produces<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json");

        endpoints.MapGet("/api/v1/products/{productId}", GetByIdAsync)
            .WithName("GetProductById").WithTags("Products")
            .RequireAuthorization(AuthenticationExtensions.AuthenticatedUserPolicy)
            .Produces<ProductResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");

        endpoints.MapGet("/api/v1/internal/products/{productId}", GetSnapshotAsync)
            .WithName("GetInternalProductSnapshot").WithTags("Internal")
            .WithSummary("Internal product snapshot for Billing")
            .RequireAuthorization(AuthenticationExtensions.AdminOnlyPolicy)
            .Produces<InternalProductResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(
        CreateProductRequest request,
        CreateProductHandler handler,
        InventoryMetrics metrics,
        ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var errors = ProductRequestValidator.ValidateCreate(request);
        if (errors.Count > 0)
        {
            return ValidationProblem(context, errors);
        }

        if (!AuthenticationExtensions.TryGetUserId(context.User, out var userId))
        {
            return Results.Problem(ApiProblemDetails.Create(
                context, 401, "authentication_required", "Autenticação necessária",
                "A identidade autenticada não contém autoria válida."));
        }

        var result = await handler.HandleAsync(
            new CreateProductCommand(request.Code, request.Description, request.InitialBalance, userId),
            cancellationToken);
        if (result.Status == CreateProductStatus.CodeAlreadyExists)
        {
            return Results.Problem(ApiProblemDetails.Create(
                context, 409, "product_code_already_exists", "Código já cadastrado",
                "Já existe um produto com o código informado."));
        }

        var response = Map(result.Product!);
        metrics.ProductCreated();
        LogCreated(loggerFactory.CreateLogger("ProductCreated"), response.Id, response.Code, userId, null);
        return Results.Created($"/api/v1/products/{response.Id:D}", response);
    }

    private static async Task<IResult> GetByIdAsync(
        string productId,
        GetProductByIdHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(productId, out var id) || id == Guid.Empty)
        {
            return InvalidProductId(context);
        }

        var product = await handler.HandleAsync(new GetProductByIdQuery(id), cancellationToken);
        return product is null ? ProductNotFound(context) : Results.Ok(Map(product));
    }

    private static async Task<IResult> GetSnapshotAsync(
        string productId,
        GetProductSnapshotHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(productId, out var id) || id == Guid.Empty)
        {
            return InvalidProductId(context);
        }

        var product = await handler.HandleAsync(new GetProductSnapshotQuery(id), cancellationToken);
        return product is null
            ? ProductNotFound(context)
            : Results.Ok(new InternalProductResponse(product.Id, product.Code, product.Description));
    }

    private static async Task<IResult> ListAsync(
        int? pageNumber,
        int? pageSize,
        ListProductsHandler handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var effectivePageNumber = pageNumber ?? 1;
        var effectivePageSize = pageSize ?? 20;
        var errors = ProductRequestValidator.ValidatePage(effectivePageNumber, effectivePageSize);
        if (errors.Count > 0)
        {
            return ValidationProblem(context, errors);
        }

        var page = await handler.HandleAsync(
            new ListProductsQuery(effectivePageNumber, effectivePageSize), cancellationToken);
        return Results.Ok(new ProductPageResponse(
            page.Items.Select(Map).ToArray(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.TotalPages));
    }

    private static ProductResponse Map(ProductDetails product) => new(
        product.Id, product.Code, product.Description, product.Balance,
        product.CreatedAtUtc, product.UpdatedAtUtc);

    private static IResult InvalidProductId(HttpContext context) => Results.Problem(
        ApiProblemDetails.Create(context, 400, "invalid_product_id", "Identificador inválido",
            "Informe um identificador de produto válido."));

    private static IResult ProductNotFound(HttpContext context) => Results.Problem(
        ApiProblemDetails.Create(context, 404, "product_not_found", "Produto não encontrado",
            "O produto informado não foi encontrado."));

    private static IResult ValidationProblem(
        HttpContext context,
        IDictionary<string, string[]> errors) => Results.ValidationProblem(
        errors,
        type: "urn:korp:problem:validation-failed",
        title: "Dados inválidos",
        statusCode: StatusCodes.Status400BadRequest,
        detail: "Revise os campos informados.",
        instance: context.Request.Path,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "validation_failed",
            ["traceId"] = context.TraceIdentifier
        });
}

public static class ProductRequestValidator
{
    public static Dictionary<string, string[]> ValidateCreate(CreateProductRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var code = request.Code?.Trim() ?? string.Empty;
        if (code.Length is < 1 or > 50
            || code.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
        {
            errors["code"] = ["Informe um código válido com até 50 caracteres."];
        }

        var description = request.Description?.Trim() ?? string.Empty;
        if (description.Length is < 1 or > 200 || description.Any(char.IsControl))
        {
            errors["description"] = ["Informe uma descrição válida com até 200 caracteres."];
        }

        if (request.InitialBalance < 0)
        {
            errors["initialBalance"] = ["O saldo inicial não pode ser negativo."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidatePage(int pageNumber, int pageSize)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (pageNumber < 1) errors["pageNumber"] = ["A página deve ser maior ou igual a 1."];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["O tamanho da página deve estar entre 1 e 100."];
        return errors;
    }
}
