using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
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
            using var envelope = JsonDocument.Parse(outbox.Payload);
            Assert.Equal("billing", envelope.RootElement.GetProperty("producer").GetString());
        }

        using var replayRequest = CreatePrintRequest(created.Id, key, prePrintEtag!, Guid.NewGuid());
        var replayResponse = await _client.SendAsync(replayRequest, TestContext.Current.CancellationToken);
        var replay = await replayResponse.Content.ReadFromJsonAsync<InvoiceIssuanceProcessResponse>(ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, replayResponse.StatusCode);
        Assert.Equal(process.Id, replay!.Id);
        var processQuery = await _client.GetAsync($"/api/v1/invoice-issuance-processes/{process.Id:D}",
            TestContext.Current.CancellationToken);
        var queried = await processQuery.Content.ReadFromJsonAsync<InvoiceIssuanceProcessResponse>(
            ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        Assert.Equal(process.Id, queried!.Id);
        await using var replayScope = _factory.Services.CreateAsyncScope();
        var replayContext = replayScope.ServiceProvider.GetRequiredService<BillingDbContext>();
        Assert.Equal(1, await replayContext.OutboxMessages.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DetailListUpdateAndRemoveRespectEtagAndStableContracts()
    {
        Authorize("Admin");
        var (invoice, item, etag) = await CreateInvoiceWithItemAsync();

        var detailResponse = await _client.GetAsync($"/api/v1/invoices/{invoice.Id:D}", TestContext.Current.CancellationToken);
        var listResponse = await _client.GetAsync("/api/v1/invoices?pageNumber=1&pageSize=20", TestContext.Current.CancellationToken);
        var page = await listResponse.Content.ReadFromJsonAsync<InvoicePageResponse>(ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Single(page!.Items);
        Assert.Equal(1, page.Items[0].ItemCount);

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/invoices/{invoice.Id:D}/items/{item.Id:D}")
        { Content = JsonContent.Create(new UpdateInvoiceItemRequest(4)) };
        update.Headers.TryAddWithoutValidation("If-Match", etag);
        var updatedResponse = await _client.SendAsync(update, TestContext.Current.CancellationToken);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<InvoiceResponse>(ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        var updatedEtag = updatedResponse.Headers.ETag!.Tag;
        Assert.Equal(4, updated!.Items.Single().Quantity);

        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/invoices/{invoice.Id:D}/items/{item.Id:D}")
        { Content = JsonContent.Create(new UpdateInvoiceItemRequest(5)) };
        stale.Headers.TryAddWithoutValidation("If-Match", etag);
        Assert.Equal(HttpStatusCode.PreconditionFailed,
            (await _client.SendAsync(stale, TestContext.Current.CancellationToken)).StatusCode);

        using var remove = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/invoices/{invoice.Id:D}/items/{item.Id:D}");
        remove.Headers.TryAddWithoutValidation("If-Match", updatedEtag);
        var removed = await _client.SendAsync(remove, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.NotNull(removed.Headers.ETag);
    }

    [Fact]
    public async Task InvalidRoutesBodiesPagingAndPrintHeadersAreRejectedBeforeEffects()
    {
        Authorize("Admin");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync("/api/v1/invoices/not-a-guid", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync("/api/v1/invoices?pageNumber=0&pageSize=101", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.GetAsync("/api/v1/invoice-issuance-processes/not-a-guid", TestContext.Current.CancellationToken)).StatusCode);

        var invoice = await CreateInvoiceAsync();
        using var invalidItem = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{invoice.Id:D}/items")
        { Content = JsonContent.Create(new AddInvoiceItemRequest(Guid.Empty, 0)) };
        invalidItem.Headers.TryAddWithoutValidation("If-Match", (await GetInvoiceEtag(invoice.Id)));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.SendAsync(invalidItem, TestContext.Current.CancellationToken)).StatusCode);

        using var missingKey = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{invoice.Id:D}/print");
        missingKey.Headers.TryAddWithoutValidation("If-Match", await GetInvoiceEtag(invoice.Id));
        var missingKeyResponse = await _client.SendAsync(missingKey, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingKeyResponse.StatusCode);
        Assert.Contains("idempotency_key_required",
            await missingKeyResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyInvoiceCannotPrintAndViewerCannotMutate()
    {
        Authorize("Admin");
        var invoice = await CreateInvoiceAsync();
        var etag = await GetInvoiceEtag(invoice.Id);
        using var print = CreatePrintRequest(invoice.Id, Guid.NewGuid(), etag, Guid.NewGuid());
        var empty = await _client.SendAsync(print, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);
        Assert.Contains("invoice_empty", await empty.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);

        Authorize("Viewer");
        var forbidden = await _client.PostAsync("/api/v1/invoices", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task ConcurrentPrintRequestsPreserveIdempotencyAndSingleActiveProcess()
    {
        Authorize("Admin");
        var (sameInvoice, _, sameEtag) = await CreateInvoiceWithItemAsync();
        var sharedKey = Guid.NewGuid();
        using var sameFirst = CreatePrintRequest(sameInvoice.Id, sharedKey, sameEtag, Guid.NewGuid());
        using var sameSecond = CreatePrintRequest(sameInvoice.Id, sharedKey, sameEtag, Guid.NewGuid());
        var sameResponses = await Task.WhenAll(
            _client.SendAsync(sameFirst, TestContext.Current.CancellationToken),
            _client.SendAsync(sameSecond, TestContext.Current.CancellationToken));
        Assert.All(sameResponses, response => Assert.Equal(HttpStatusCode.Accepted, response.StatusCode));
        var firstProcess = await sameResponses[0].Content.ReadFromJsonAsync<InvoiceIssuanceProcessResponse>(
            ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        var secondProcess = await sameResponses[1].Content.ReadFromJsonAsync<InvoiceIssuanceProcessResponse>(
            ApiJsonOptions.Create(), TestContext.Current.CancellationToken);
        Assert.Equal(firstProcess!.Id, secondProcess!.Id);

        var (differentInvoice, _, differentEtag) = await CreateInvoiceWithItemAsync();
        using var differentFirst = CreatePrintRequest(differentInvoice.Id, Guid.NewGuid(), differentEtag, Guid.NewGuid());
        using var differentSecond = CreatePrintRequest(differentInvoice.Id, Guid.NewGuid(), differentEtag, Guid.NewGuid());
        var differentResponses = await Task.WhenAll(
            _client.SendAsync(differentFirst, TestContext.Current.CancellationToken),
            _client.SendAsync(differentSecond, TestContext.Current.CancellationToken));
        Assert.Single(differentResponses, response => response.StatusCode == HttpStatusCode.Accepted);
        Assert.Single(differentResponses, response => response.StatusCode == HttpStatusCode.Conflict);

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        Assert.Equal(2, await context.OutboxMessages.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.InvoiceIssuanceProcesses.CountAsync(TestContext.Current.CancellationToken));
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

    private async Task<(InvoiceResponse Invoice, InvoiceItemResponse Item, string Etag)> CreateInvoiceWithItemAsync()
    {
        var invoice = await CreateInvoiceAsync();
        var etag = await GetInvoiceEtag(invoice.Id);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{invoice.Id:D}/items")
        { Content = JsonContent.Create(new AddInvoiceItemRequest(ProductId, 2)) };
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<InvoiceResponse>(
            ApiJsonOptions.Create(), TestContext.Current.CancellationToken))!;
        return (updated, updated.Items.Single(), response.Headers.ETag!.Tag);
    }

    private async Task<string> GetInvoiceEtag(Guid invoiceId)
    {
        var response = await _client.GetAsync($"/api/v1/invoices/{invoiceId:D}", TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return response.Headers.ETag!.Tag;
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
