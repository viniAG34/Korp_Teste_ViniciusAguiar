using Korp.Billing.Domain.Invoices;

namespace Korp.Billing.Application.Invoices;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken);
    void Add(Invoice invoice);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IInvoiceReadService
{
    Task<InvoiceDetails?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken);
    Task<InvoicePage> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
}

public interface IInvoiceNumberGenerator
{
    Task<long> GetNextAsync(CancellationToken cancellationToken);
}

public sealed record ProductSnapshot(Guid Id, string Code, string Description);

public interface IProductCatalogClient
{
    Task<ProductSnapshot?> GetSnapshotAsync(Guid productId, CancellationToken cancellationToken);
}

public sealed class ProductCatalogUnavailableException(Exception? innerException = null)
    : Exception("Product catalog is unavailable.", innerException);
