using System.Net;
using System.Text.Json;
using Korp.Billing.Api.Correlation;
using Korp.Billing.Api.ProductCatalog;
using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Infrastructure.ProductCatalog;
using Microsoft.AspNetCore.Http;

namespace Korp.Billing.IntegrationTests.ProductCatalog;

public sealed class ProductCatalogClientTests
{
    [Fact]
    public async Task ValidSnapshotIsReturnedWithoutRetry()
    {
        var productId = Guid.NewGuid();
        var handler = new StubHandler((_, _) => Response(HttpStatusCode.OK,
            new { id = productId, code = "PROD-1", description = "Produto" }));
        var telemetry = new TelemetryFake();

        var snapshot = await CreateClient(handler, telemetry)
            .GetSnapshotAsync(productId, TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot);
        Assert.Equal("PROD-1", snapshot.Code);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(["success"], telemetry.CatalogOutcomes);
    }

    [Fact]
    public async Task NotFoundIsFunctionalAndIsNotRetried()
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.NotFound));
        var telemetry = new TelemetryFake();

        var snapshot = await CreateClient(handler, telemetry)
            .GetSnapshotAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(snapshot);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal(["not_found"], telemetry.CatalogOutcomes);
    }

    [Fact]
    public async Task ServerFailureIsRetriedOnceAndCanRecover()
    {
        var productId = Guid.NewGuid();
        var handler = new StubHandler((call, _) => call == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Response(HttpStatusCode.OK, new { id = productId, code = "P-1", description = "Produto" }));

        var snapshot = await CreateClient(handler, new TelemetryFake())
            .GetSnapshotAsync(productId, TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task ClientErrorsBecomeUnavailableWithoutRetry(HttpStatusCode status)
    {
        var handler = new StubHandler((_, _) => new HttpResponseMessage(status));

        await Assert.ThrowsAsync<ProductCatalogUnavailableException>(() => CreateClient(handler, new TelemetryFake())
            .GetSnapshotAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task InvalidContractIsRejectedBeforePersistenceBoundary()
    {
        var productId = Guid.NewGuid();
        var handler = new StubHandler((_, _) => Response(HttpStatusCode.OK,
            new { id = productId, code = " ", description = "Produto" }));

        await Assert.ThrowsAsync<ProductCatalogUnavailableException>(() => CreateClient(handler, new TelemetryFake())
            .GetSnapshotAsync(productId, TestContext.Current.CancellationToken));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AuthorizationAndCorrelationAreForwardedAtTheHttpBoundary()
    {
        var correlation = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer sensitive-token";
        context.Items[CorrelationMiddleware.ItemName] = correlation;
        var capture = new CaptureHandler();
        var forwarding = new ForwardAuthorizationHandler(new HttpContextAccessor { HttpContext = context })
        {
            InnerHandler = capture
        };
        using var client = new HttpClient(forwarding);

        using var response = await client.GetAsync("http://inventory.test/api/v1/internal/products/1",
            TestContext.Current.CancellationToken);

        Assert.Equal("Bearer sensitive-token", capture.Authorization);
        Assert.Equal(correlation.ToString("D"), capture.Correlation);
    }

    private static ProductCatalogClient CreateClient(HttpMessageHandler handler, IBillingTelemetry telemetry) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://inventory.test") }, telemetry);

    private static HttpResponseMessage Response(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<int, HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(response(CallCount, request));
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public string? Correlation { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            Correlation = request.Headers.GetValues(CorrelationMiddleware.HeaderName).Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class TelemetryFake : IBillingTelemetry
    {
        public List<string> CatalogOutcomes { get; } = [];
        public void ProductCatalogRequest(string outcome) => CatalogOutcomes.Add(outcome);
        public void InvoiceCreated() { }
        public void ItemOperation(string operation, string outcome) { }
        public void IssuanceRequested(string outcome) { }
        public void IssuanceTransitioned(string status) { }
    }
}
