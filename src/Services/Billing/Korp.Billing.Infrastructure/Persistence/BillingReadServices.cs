using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Application.Issuance;
using Microsoft.EntityFrameworkCore;

namespace Korp.Billing.Infrastructure.Persistence;

public sealed class InvoiceReadService(BillingDbContext context) : IInvoiceReadService
{
    public async Task<InvoiceDetails?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        try
        {
            var invoice = await context.Invoices.AsNoTracking().Include(value => value.Items)
                .SingleOrDefaultAsync(value => value.Id == invoiceId, cancellationToken);
            return invoice?.ToDetails();
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        { throw new BillingServiceUnavailableException(exception); }
    }

    public async Task<InvoicePage> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        try
        {
            var query = context.Invoices.AsNoTracking();
            var count = await query.LongCountAsync(cancellationToken);
            var items = await query.OrderByDescending(invoice => invoice.CreatedAtUtc).ThenBy(invoice => invoice.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(invoice => new InvoiceSummary(
                    invoice.Id, invoice.Number, invoice.Status, invoice.IsIssuanceInProgress,
                    invoice.Items.Count, invoice.CreatedAtUtc, invoice.UpdatedAtUtc))
                .ToArrayAsync(cancellationToken);
            var pages = count == 0 ? 0 : (int)Math.Ceiling((double)count / pageSize);
            return new InvoicePage(items, pageNumber, pageSize, count, pages);
        }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        { throw new BillingServiceUnavailableException(exception); }
    }
}

public sealed class IssuanceProcessReadService(BillingDbContext context) : IIssuanceProcessReadService
{
    public Task<PersistedIssuanceProcess?> GetByIdAsync(Guid processId, CancellationToken cancellationToken) =>
        Project(context.InvoiceIssuanceProcesses.AsNoTracking().Where(process => process.Id == processId))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PersistedIssuanceProcess?> GetByIdempotencyKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
        Project(context.InvoiceIssuanceProcesses.AsNoTracking().Where(process => process.IdempotencyKey == idempotencyKey))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PersistedIssuanceProcess?> GetActiveByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        Project(context.InvoiceIssuanceProcesses.AsNoTracking().Where(process => process.InvoiceId == invoiceId
            && (process.Status == Domain.Issuance.InvoiceIssuanceProcessStatus.Pending
                || process.Status == Domain.Issuance.InvoiceIssuanceProcessStatus.AwaitingStock)))
            .SingleOrDefaultAsync(cancellationToken);

    private IQueryable<PersistedIssuanceProcess> Project(IQueryable<Domain.Issuance.InvoiceIssuanceProcess> processes) =>
        from process in processes
        join invoice in context.Invoices.AsNoTracking() on process.InvoiceId equals invoice.Id
        select new PersistedIssuanceProcess(
            process.Id, process.InvoiceId, process.IdempotencyKey, process.Status,
            process.CreatedAtUtc, process.UpdatedAtUtc, process.FinishedAtUtc,
            process.OutcomeCode, process.OutcomeDescription, invoice.Version);
}
