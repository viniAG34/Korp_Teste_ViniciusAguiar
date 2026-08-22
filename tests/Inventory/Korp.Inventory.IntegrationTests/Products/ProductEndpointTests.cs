using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Korp.Inventory.Api.Features.Products.Contracts;
using Korp.Inventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Korp.Inventory.IntegrationTests.Products;

public sealed class ProductEndpointTests : IAsyncLifetime
{
    private static readonly byte[] SigningKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private readonly string _connectionString = Environment.GetEnvironmentVariable("INVENTORY_TEST_CONNECTION")
        ?? "Host=localhost;Port=5433;Database=inventory_db;Username=inventory;Password=inventory_test_password";
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = CreateFactory(_connectionString);
        _client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE stock_movements, products, inbox_messages, outbox_messages CASCADE",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync();
    }

    [Fact]
    public async Task TstInv006AdminCreatesNormalizedProductWithAuthorshipAndNoMovement()
    {
        Authorize("Admin");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductRequest(" prod-001 ", " Product ", 0),
            TestContext.Current.CancellationToken);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(product);
        Assert.Equal("PROD-001", product.Code);
        Assert.Equal("Product", product.Description);
        Assert.Equal(0, product.Balance);
        Assert.Equal($"/api/v1/products/{product.Id:D}", response.Headers.Location?.OriginalString);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var persisted = await context.Products.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TestUserId, persisted.CreatedByUserId);
        Assert.Equal(0, await context.StockMovements.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstInv007EquivalentCodeReturnsStableConflict()
    {
        Authorize("Admin");
        await CreateProductAsync("prod-001", "First", 1);

        var duplicate = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductRequest(" PROD-001 ", "Second", 1),
            TestContext.Current.CancellationToken);
        var body = await duplicate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("product_code_already_exists", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidCreateContractReturnsValidationProblemWithoutPersistence()
    {
        Authorize("Admin");

        var response = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductRequest("invalid code", "Invalid\nDescription", -1),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("validation_failed", body, StringComparison.Ordinal);
        Assert.Contains("code", body, StringComparison.Ordinal);
        Assert.Contains("description", body, StringComparison.Ordinal);
        Assert.Contains("initialBalance", body, StringComparison.Ordinal);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        Assert.Equal(0, await context.Products.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TstInv009And010QueriesAreOrderedPaginatedAndReturnApprovedErrors()
    {
        Authorize("Admin");
        var second = await CreateProductAsync("B-002", "Second", 2);
        await CreateProductAsync("A-001", "First", 1);
        Authorize("Viewer");

        var page = await _client.GetFromJsonAsync<ProductPageResponse>(
            "/api/v1/products?pageNumber=1&pageSize=1",
            TestContext.Current.CancellationToken);
        var found = await _client.GetAsync($"/api/v1/products/{second.Id:D}", TestContext.Current.CancellationToken);
        var invalid = await _client.GetAsync("/api/v1/products/not-a-guid", TestContext.Current.CancellationToken);
        var missing = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid():D}", TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal("A-001", page.Items.Single().Code);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task TstInv011InternalSnapshotExcludesInventoryState()
    {
        Authorize("Admin");
        var product = await CreateProductAsync("P-1", "Product", 9);

        var response = await _client.GetAsync(
            $"/api/v1/internal/products/{product.Id:D}",
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("P-1", body, StringComparison.Ordinal);
        Assert.DoesNotContain("balance", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdAt", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TstInv024PoliciesDifferentiateUnauthorizedAndForbidden()
    {
        var unauthorized = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductRequest("P-1", "Product", 0),
            TestContext.Current.CancellationToken);
        Authorize("Viewer");
        var forbidden = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductRequest("P-1", "Product", 0),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task TstInv026OpenApiContainsOnlyApprovedInventoryRoutes()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/v1/products", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/internal/products/{productId}", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/deduct", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/adjust", document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TstDst022HealthEndpointsSeparateHttpReadinessFromMessagingDependencies()
    {
        var live = await _client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        var ready = await _client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var dependencies = await _client.GetAsync("/health/dependencies", TestContext.Current.CancellationToken);
        var liveBody = await live.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var readyBody = await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var dependenciesBody = await dependencies.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, dependencies.StatusCode);
        Assert.Contains("self", liveBody, StringComparison.Ordinal);
        Assert.DoesNotContain("database", liveBody, StringComparison.Ordinal);
        Assert.Contains("configuration", readyBody, StringComparison.Ordinal);
        Assert.Contains("database", readyBody, StringComparison.Ordinal);
        Assert.DoesNotContain("rabbitmq", readyBody, StringComparison.Ordinal);
        Assert.Contains("rabbitmq", dependenciesBody, StringComparison.Ordinal);
        Assert.Contains("topology", dependenciesBody, StringComparison.Ordinal);
        Assert.Contains("dispatcher", dependenciesBody, StringComparison.Ordinal);
        Assert.Contains("consumer", dependenciesBody, StringComparison.Ordinal);
        Assert.DoesNotContain("inventory_test_password", dependenciesBody, StringComparison.Ordinal);
        Assert.DoesNotContain("localhost", dependenciesBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TstDst021HostUsesApprovedShutdownDeadline()
    {
        var options = _factory.Services.GetRequiredService<IOptions<HostOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(30), options.ShutdownTimeout);
    }

    private static readonly Guid TestUserId = Guid.Parse("a2bca719-cfeb-4bf4-b8b4-317af2181012");

    private void Authorize(string role) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(role));

    private async Task<ProductResponse> CreateProductAsync(string code, string description, int balance)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new CreateProductRequest(code, description, balance),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken))!;
    }

    private static string CreateToken(string role)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim("sub", TestUserId.ToString("D")),
                new Claim("email", "admin@korp.local"),
                new Claim("role", role)]),
            Issuer = "korp-identity",
            Audience = "korp-erp-api",
            IssuedAt = now,
            NotBefore = now.AddSeconds(-5),
            Expires = now.AddMinutes(15),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(SigningKey),
                SecurityAlgorithms.HmacSha256)
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:InventoryDatabase", connectionString);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(SigningKey));
            builder.UseSetting("Jwt:Issuer", "korp-identity");
            builder.UseSetting("Jwt:Audience", "korp-erp-api");
        });
}
