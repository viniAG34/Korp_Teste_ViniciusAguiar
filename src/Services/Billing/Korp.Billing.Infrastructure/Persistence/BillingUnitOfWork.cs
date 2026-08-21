using System.Data;
using System.Text.Json;
using Korp.Billing.Application.Common;
using Korp.Billing.Application.Issuance;
using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;
using Korp.Billing.Infrastructure.Persistence.Messaging;
using Korp.Integration.Contracts.Events;
using Korp.Integration.Contracts.StockDeduction.V1;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Korp.Billing.Infrastructure.Persistence;

public sealed class BillingUnitOfWork(
    BillingDbContext context, IDbContextTransaction transaction) : IBillingUnitOfWork
{
    public Task<Invoice?> GetInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        context.Invoices.Include(invoice => invoice.Items)
            .SingleOrDefaultAsync(invoice => invoice.Id == invoiceId, cancellationToken);

    public Task<InvoiceIssuanceProcess?> GetProcessByIdAsync(Guid processId, CancellationToken cancellationToken) =>
        context.InvoiceIssuanceProcesses.SingleOrDefaultAsync(process => process.Id == processId, cancellationToken);

    public Task<InvoiceIssuanceProcess?> GetProcessByKeyAsync(Guid idempotencyKey, CancellationToken cancellationToken) =>
        context.InvoiceIssuanceProcesses.SingleOrDefaultAsync(process => process.IdempotencyKey == idempotencyKey, cancellationToken);

    public void AddProcess(InvoiceIssuanceProcess process) => context.InvoiceIssuanceProcesses.Add(process);

    public void AddOutbox(StockDeductionOutboxRequest request)
    {
        var payload = new StockDeductionRequestedV1(
            request.IssuanceProcessId, request.InvoiceId, request.InvoiceNumber, request.RequestedByUserId,
            request.Items.Select(item => new StockDeductionRequestItemV1(item.ProductId, item.Quantity)).ToArray());
        var envelope = new IntegrationEventEnvelope<StockDeductionRequestedV1>(
            request.MessageId, IntegrationEventTypes.StockDeductionRequested, 1,
            request.OccurredAtUtc, request.CorrelationId, null, "billing-service", payload);
        var json = JsonSerializer.Serialize(envelope, JsonSerializerOptions.Web);
        context.OutboxMessages.Add(OutboxMessage.Create(
            request.MessageId, IntegrationEventTypes.StockDeductionRequested, 1,
            json, request.CorrelationId, null, request.OccurredAtUtc));
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) { throw new BillingConcurrencyException(exception); }
        catch (DbUpdateException exception) when (InvoiceRepository.KnownConstraint(exception) is { } constraint)
        { throw new BillingConstraintException(constraint, exception); }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        { throw new BillingServiceUnavailableException(exception); }
    }

    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }
}

public sealed class BillingUnitOfWorkFactory(IDbContextFactory<BillingDbContext> factory) : IBillingUnitOfWorkFactory
{
    public async Task<IBillingUnitOfWork> CreateAsync(CancellationToken cancellationToken)
    {
        var context = await factory.CreateDbContextAsync(cancellationToken);
        try
        {
            var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            return new BillingUnitOfWork(context, transaction);
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }
}
