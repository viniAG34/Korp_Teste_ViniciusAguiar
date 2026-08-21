using Korp.Inventory.Application.Common;
using Korp.Inventory.Domain.Products;

namespace Korp.Inventory.Application.Products;

public sealed record CreateProductCommand(
    string Code,
    string Description,
    int InitialBalance,
    Guid CreatedByUserId);

public enum CreateProductStatus { Created, CodeAlreadyExists }

public sealed record CreateProductResult(CreateProductStatus Status, ProductDetails? Product)
{
    public static CreateProductResult AlreadyExists() => new(CreateProductStatus.CodeAlreadyExists, null);
    public static CreateProductResult Created(ProductDetails product) => new(CreateProductStatus.Created, product);
}

public sealed class CreateProductHandler(
    IProductRepository repository,
    IGuidGenerator guidGenerator,
    TimeProvider timeProvider)
{
    public async Task<CreateProductResult> HandleAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedCode = ProductCode.Create(command.Code).Value;
        if (await repository.CodeExistsAsync(normalizedCode, cancellationToken))
        {
            return CreateProductResult.AlreadyExists();
        }

        var product = Product.Create(
            guidGenerator.NewGuid(),
            normalizedCode,
            command.Description,
            command.InitialBalance,
            command.CreatedByUserId,
            timeProvider.GetUtcNow());
        repository.Add(product);
        try
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch (ProductCodeAlreadyExistsException)
        {
            return CreateProductResult.AlreadyExists();
        }

        return CreateProductResult.Created(new ProductDetails(
            product.Id,
            product.Code.Value,
            product.Description,
            product.Balance,
            product.CreatedAtUtc,
            product.UpdatedAtUtc));
    }
}

public sealed class ProductCodeAlreadyExistsException(Exception? innerException = null)
    : Exception("Product code already exists.", innerException);
