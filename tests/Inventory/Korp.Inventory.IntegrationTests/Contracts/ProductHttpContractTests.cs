using System.Text.Json;
using Korp.Inventory.Api.Features.Products.Contracts;
using Korp.Inventory.Api.Http;

namespace Korp.Inventory.IntegrationTests.Contracts;

public sealed class ProductHttpContractTests
{
    [Fact]
    public void CreateRequestCannotSetServerOwnedFields()
    {
        var properties = typeof(CreateProductRequest).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(["Code", "Description", "InitialBalance"], properties);
    }

    [Fact]
    public void InternalProductContractDoesNotExposeInventoryState()
    {
        var properties = typeof(InternalProductResponse).GetProperties().Select(property => property.Name).ToArray();

        Assert.Equal(["Id", "Code", "Description"], properties);
        Assert.DoesNotContain("Balance", properties);
        Assert.DoesNotContain("UpdatedAtUtc", properties);
    }

    [Fact]
    public void ProductPageUsesApprovedPaginationShape()
    {
        var product = new ProductResponse(
            Guid.NewGuid(),
            "PROD-001",
            "Produto",
            10,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var page = new ProductPageResponse([product], 1, 20, 1, 1);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(page, ApiJsonOptions.Create()));

        Assert.Equal(1, json.RootElement.GetProperty("pageNumber").GetInt32());
        Assert.Equal(20, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("items").GetArrayLength());
    }
}
