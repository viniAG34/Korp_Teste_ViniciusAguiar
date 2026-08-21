using System.Net;
using System.Net.Http.Json;
using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;

namespace Korp.Billing.Infrastructure.ProductCatalog;

public sealed class ProductCatalogClient(HttpClient httpClient, IBillingTelemetry telemetry) : IProductCatalogClient
{
    public async Task<ProductSnapshot?> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                using var response = await httpClient.GetAsync(
                    $"/api/v1/internal/products/{productId:D}", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    telemetry.ProductCatalogRequest("not_found");
                    return null;
                }

                if ((int)response.StatusCode >= 500 && attempt == 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new ProductCatalogUnavailableException();

                var snapshot = await response.Content.ReadFromJsonAsync<InternalProductSnapshot>(cancellationToken);
                if (snapshot is null || snapshot.Id != productId
                    || string.IsNullOrWhiteSpace(snapshot.Code) || snapshot.Code.Length > 50
                    || string.IsNullOrWhiteSpace(snapshot.Description) || snapshot.Description.Length > 200
                    || snapshot.Code != snapshot.Code.Trim() || snapshot.Description != snapshot.Description.Trim())
                    throw new ProductCatalogUnavailableException();

                telemetry.ProductCatalogRequest("success");
                return new ProductSnapshot(snapshot.Id, snapshot.Code, snapshot.Description);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt == 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (HttpRequestException) when (attempt == 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
            {
                telemetry.ProductCatalogRequest("unavailable");
                throw new ProductCatalogUnavailableException(exception);
            }
        }

        throw new ProductCatalogUnavailableException();
    }

    private sealed record InternalProductSnapshot(Guid Id, string Code, string Description);
}
