using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Korp.Billing.Api.Features.Invoices.Contracts;
using Korp.Billing.Api.Features.Issuance.Contracts;
using Korp.Billing.Api.Http;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Application.Issuance;
using Korp.Billing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Korp.Billing.IntegrationTests.Invoices;

public sealed class BillingEndpointTests : IAsyncLifetime
{
    private static readonly byte[] SigningKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
    private static readonly Guid UserId = Guid.Parse("c47caac2-6b8c-40f7-a9bf-99e5f84bc404");
    private static readonly Guid ProductId = Guid.Parse("9d68ebdf-675f-4e24-a36e-140592a52c45");
    private readonly string _connectionString = Environment.GetEnvironmentVariable("BILLING_TEST_CONNECTION")
        ?? "Host=localhost;Port=5434;Database=billing_db;Username=billing;Password=billing_test_password";
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = CreateFactory(_connectionString);
        _client = _factory.CreateClient();
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE invoice_items, invoice_issuance_processes, invoices, inbox_messages, outbox_messages CASCADE",
            TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateAddAndPrintPersistDurableIntentAndReplayOriginalProcess()
    {
        Authorize("Admin");
        var createdResponse = await _client.PostAsync("/api/v1/invoices", null, TestContext.Current.CancellationToken);
        var created = await createdResponse.Content.ReadFromJsonAsync<InvoiceResponse>(ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        var createEtag = createdResponse.Headers.ETag?.Tag;

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotNull(createEtag);
        Assert.Empty(created.Items);
        await using (var versionScope = _factory.Services.CreateAsyncScope())
        {
            var versionContext = versionScope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var storedVersion = await versionContext.Invoices.AsNoTracking()
                .Where(invoice => invoice.Id == created.Id)
                .Select(invoice => invoice.Version)
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(storedVersion, EntityTag.Parse(createEtag).Value?.Version);
            var repository = versionScope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var tracked = await repository.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);
            Assert.Equal(storedVersion, tracked?.Version);
        }

        using var addRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{created.Id:D}/items")
        {
            Content = JsonContent.Create(new AddInvoiceItemRequest(ProductId, 2))
        };
        addRequest.Headers.TryAddWithoutValidation("If-Match", createEtag);
        var addResponse = await _client.SendAsync(addRequest, TestContext.Current.CancellationToken);
        var added = await addResponse.Content.ReadFromJsonAsync<InvoiceResponse>(ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        var prePrintEtag = addResponse.Headers.ETag?.Tag;

        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        Assert.Single(added!.Items);
        Assert.Equal("PROD-001", added.Items[0].ProductCode);

        var key = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        using var printRequest = CreatePrintRequest(created.Id, key, prePrintEtag!, correlation);
        var printResponse = await _client.SendAsync(printRequest, TestContext.Current.CancellationToken);
        var process = await printResponse.Content.ReadFromJsonAsync<InvoiceIssuanceProcessResponse>(ApiJsonOptions.Create(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, printResponse.StatusCode);
        Assert.Equal(InvoiceIssuanceProcessStatusResponse.Pending, process!.Status);
        Assert.Equal("1", printResponse.Headers.RetryAfter?.ToString());
        Assert.Equal($"/api/v1/invoice-issuance-processes/{process.Id:D}", printResponse.Headers.Location?.OriginalString);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var invoice = await context.Invoices.SingleAsync(TestContext.Current.CancellationToken);
            var outbox = await context.OutboxMessages.SingleAsync(TestContext.Current.CancellationToken);
            Assert.True(invoice.IsIssuanceInProgress);
            Assert.Equal(correlation, outbox.CorrelationId);
            Assert.Contains(process.Id.ToString("D"), outbox.Payload, StringComparison.OrdinalIgnoreCase);
        }

        using var replayRequest = CreatePrintRequest(created.Id, key, prePrintEtag!, Guid.NewGuid());
        var replayResponse = await _client.SendAsync(replayRequest, TestContext.Current.CancellationToken);
        var replay = await replayResponse.Content.ReadFromJsonAsync<InvoiceIssuanceProcessResponse>(ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, replayResponse.StatusCode);
        Assert.Equal(process.Id, replay!.Id);
        await using var replayScope = _factory.Services.CreateAsyncScope();
        var replayContext = replayScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        Assert.Equal(1, await replayContext.OutboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SecurityCorrelationAndPreconditionErrorsUseStableCodes()
    {
        var unauthorized = await _client.GetAsync("/api/v1/invoices", TestContext.Current.CancellationToken);
        Authorize("Admin");
        using var invalidCorrelation = new HttpRequestMessage(HttpMethod.Get, "/api/v1/invoices");
        invalidCorrelation.Headers.TryAddWithoutValidation("X-Correlation-ID", "invalid");
        var invalidCorrelationResponse = await _client.SendAsync(invalidCorrelation, TestContext.Current.CancellationToken);
        var invoice = await CreateInvoiceAsync();
        var missingVersion = await _client.PostAsJsonAsync($"/api/v1/invoices/{invoice.Id:D}/items",
            new AddInvoiceItemRequest(ProductId, 1), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Contains("authentication_required", await unauthorized.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, invalidCorrelationResponse.StatusCode);
        Assert.Contains("invalid_correlation_id", await invalidCorrelationResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode)428, missingVersion.StatusCode);
        Assert.Contains("invoice_version_required", await missingVersion.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApiContainsExactlyTheApprovedBillingRouteFamily()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var document = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/v1/invoices/{invoiceId}/print", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/invoice-issuance-processes/{processId}", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/cancel", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/pdf", document, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<InvoiceResponse> CreateInvoiceAsync()
    {
        var response = await _client.PostAsync("/api/v1/invoices", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvoiceResponse>(ApiJsonOptions.Create(), TestContext.Current.CancellationToken))!;
    }

    private static HttpRequestMessage CreatePrintRequest(Guid invoiceId, Guid key, string etag, Guid correlation)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{invoiceId:D}/print");
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key.ToString("D"));
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlation.ToString("D"));
        return request;
    }

    private void Authorize(string role) => _client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", CreateToken(role));

    private static string CreateToken(string role)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim("sub", UserId.ToString("D")), new Claim("email", "admin@korp.local"), new Claim("role", role)]),
            Issuer = "korp-identity", Audience = "korp-erp-api", IssuedAt = now,
            NotBefore = now.AddSeconds(-5), Expires = now.AddMinutes(15),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(SigningKey), SecurityAlgorithms.HmacSha256)
        });
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:BillingDatabase", connectionString);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(SigningKey));
            builder.UseSetting("Jwt:Issuer", "korp-identity");
            builder.UseSetting("Jwt:Audience", "korp-erp-api");
            builder.UseSetting("Services:InventoryBaseUrl", "http://inventory.test");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProductCatalogClient>();
                services.AddScoped<IProductCatalogClient, ProductCatalogFake>();
            });
        });

    private sealed class ProductCatalogFake : IProductCatalogClient
    {
        public Task<ProductSnapshot?> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken) =>
            Task.FromResult<ProductSnapshot?>(productId == ProductId
                ? new ProductSnapshot(ProductId, "PROD-001", "Produto de teste") : null);
    }
}
