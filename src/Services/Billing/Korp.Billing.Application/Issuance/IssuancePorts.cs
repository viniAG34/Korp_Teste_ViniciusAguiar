using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;

namespace Korp.Billing.Application.Issuance;

public interface IInvoiceIssuanceProcessRepository
{
    Task<InvoiceIssuanceProcess?> GetByIdAsync(Guid processId, CancellationToken cancellationToken);
    Task<InvoiceIssuanceProcess?> GetByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken);
    void Add(InvoiceIssuanceProcess process);
}

public interface IIssuanceProcessReadService
{
    Task<PersistedIssuanceProcess?> GetByIdAsync(Guid processId, CancellationToken cancellationToken);
    Task<PersistedIssuanceProcess?> GetByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken);
    Task<PersistedIssuanceProcess?> GetActiveByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken);
}

public sealed record StockDeductionOutboxRequest(
    Guid MessageId,
    Guid IssuanceProcessId,
    Guid InvoiceId,
    long InvoiceNumber,
    Guid RequestedByUserId,
    Guid CorrelationId,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyList<StockDeductionOutboxItem> Items);

public sealed record StockDeductionOutboxItem(Guid ProductId, int Quantity);

public sealed record ProcessedBillingMessage(Guid MessageId, string PayloadHash);
public sealed record ProcessedBillingMessageRequest(
    Guid MessageId, string MessageType, int SchemaVersion, Guid CorrelationId,
    Guid? CausationId, string PayloadHash, DateTimeOffset ProcessedAtUtc);

public interface IStockDeductionOutbox
{
    void Add(StockDeductionOutboxRequest request);
}

public interface IBillingUnitOfWork : IAsyncDisposable
{
    Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken);
    Task<InvoiceIssuanceProcess?> GetProcessByIdAsync(Guid processId, CancellationToken cancellationToken);
    Task<InvoiceIssuanceProcess?> GetProcessByKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken);
    Task<ProcessedBillingMessage?> GetProcessedMessageAsync(Guid messageId, CancellationToken cancellationToken);
    void AddProcess(InvoiceIssuanceProcess process);
    void AddOutbox(StockDeductionOutboxRequest request);
    void AddProcessedMessage(ProcessedBillingMessageRequest request);
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IBillingUnitOfWorkFactory
{
    Task<IBillingUnitOfWork> CreateAsync(CancellationToken cancellationToken);
}
