using Korp.Billing.Application.Common;
using Korp.Billing.Application.Invoices;
using Korp.Billing.Domain.Invoices;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Korp.Billing.Infrastructure.Persistence;

public sealed class InvoiceRepository(BillingDbContext context) : IInvoiceRepository
{
    public Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken) =>
        ExecuteAsync(() => context.Invoices.Include(invoice => invoice.Items)
            .SingleOrDefaultAsync(invoice => invoice.Id == invoiceId, cancellationToken));

    public void Add(Invoice invoice) => context.Invoices.Add(invoice);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new BillingConcurrencyException(exception); }
        catch (DbUpdateException exception) when (Constraint(exception) is { } constraint)
        { throw new BillingConstraintException(constraint, exception); }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        { throw new BillingServiceUnavailableException(exception); }
    }

    private static BillingConstraint? Constraint(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
            ? postgres.ConstraintName switch
            {
                "uq_invoice_items_invoice_id_product_id" => BillingConstraint.ProductAlreadyAdded,
                "uq_invoice_issuance_processes_idempotency_key" => BillingConstraint.IdempotencyKey,
                "uq_invoice_issuance_processes_active_invoice" => BillingConstraint.ActiveIssuance,
                _ => null
            }
            : null;

    internal static async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception exception) when (DatabaseErrorClassifier.IsUnavailable(exception))
        { throw new BillingServiceUnavailableException(exception); }
    }

    internal static BillingConstraint? KnownConstraint(DbUpdateException exception) => Constraint(exception);
}
