using Korp.Billing.Application.Common;
using Korp.Billing.Domain.Invoices;
using Korp.Billing.Domain.Issuance;

namespace Korp.Billing.Application.Issuance;

public sealed record PrintInvoiceCommand(
    Guid InvoiceId, Guid IdempotencyKey, uint ExpectedVersion, Guid RequestedByUserId, Guid CorrelationId);

public enum PrintInvoiceStatus
{
    Accepted, ReplayedActive, ReplayedTerminal, InvoiceNotFound, VersionMismatch,
    InvoiceNotOpen, InvoiceEmpty, IssuanceInProgress, IdempotencyKeyReused
}

public sealed record PrintInvoiceResult(PrintInvoiceStatus Status, IssuanceProcessDetails? Process = null);
public sealed record GetIssuanceProcessQuery(Guid ProcessId);

public sealed class PrintInvoiceHandler(
    IBillingUnitOfWorkFactory unitOfWorkFactory,
    IIssuanceProcessReadService readService,
    IGuidGenerator guidGenerator,
    TimeProvider timeProvider,
    IBillingTelemetry telemetry)
{
    public async Task<PrintInvoiceResult> HandleAsync(PrintInvoiceCommand command, CancellationToken cancellationToken)
    {
        var known = await readService.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
        if (known is not null) return Replay(known, command.InvoiceId);

        await using var unit = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var invoice = await unit.GetInvoiceAsync(command.InvoiceId, cancellationToken);
        if (invoice is null) return new(PrintInvoiceStatus.InvoiceNotFound);
        if (invoice.Version != command.ExpectedVersion) return new(PrintInvoiceStatus.VersionMismatch);
        if (invoice.Status != InvoiceStatus.Open) return new(PrintInvoiceStatus.InvoiceNotOpen);
        if (invoice.Items.Count == 0) return new(PrintInvoiceStatus.InvoiceEmpty);
        if (invoice.IsIssuanceInProgress) return new(PrintInvoiceStatus.IssuanceInProgress);

        var now = timeProvider.GetUtcNow();
        var process = InvoiceIssuanceProcess.Create(
            guidGenerator.NewGuid(), invoice.Id, command.IdempotencyKey, command.RequestedByUserId, now);
        invoice.StartIssuance(now);
        unit.AddProcess(process);
        unit.AddOutbox(new StockDeductionOutboxRequest(
            guidGenerator.NewGuid(), process.Id, invoice.Id, invoice.Number,
            command.RequestedByUserId, command.CorrelationId, now,
            invoice.Items.Select(item => new StockDeductionOutboxItem(item.ProductId, item.Quantity)).ToArray()));
        try
        {
            await unit.CommitAsync(cancellationToken);
        }
        catch (BillingConstraintException exception) when (
            exception.Constraint is BillingConstraint.IdempotencyKey or BillingConstraint.ActiveIssuance)
        {
            var winner = await readService.GetByIdempotencyKeyAsync(command.IdempotencyKey, cancellationToken);
            if (winner is not null) return Replay(winner, command.InvoiceId);
            return new(PrintInvoiceStatus.IssuanceInProgress);
        }
        catch (BillingConcurrencyException)
        {
            return new(PrintInvoiceStatus.VersionMismatch);
        }

        telemetry.IssuanceRequested("accepted");
        var persisted = await readService.GetByIdAsync(process.Id, cancellationToken)
            ?? throw new BillingConsistencyException("The accepted issuance process could not be reloaded.");
        return new(PrintInvoiceStatus.Accepted, persisted.ToDetails(timeProvider.GetUtcNow()));
    }

    private PrintInvoiceResult Replay(PersistedIssuanceProcess known, Guid invoiceId)
    {
        if (known.InvoiceId != invoiceId) return new(PrintInvoiceStatus.IdempotencyKeyReused);
        var active = known.Status is InvoiceIssuanceProcessStatus.Pending or InvoiceIssuanceProcessStatus.AwaitingStock;
        telemetry.IssuanceRequested("replayed");
        return new(active ? PrintInvoiceStatus.ReplayedActive : PrintInvoiceStatus.ReplayedTerminal,
            known.ToDetails(timeProvider.GetUtcNow()));
    }
}

public sealed class GetIssuanceProcessHandler(IIssuanceProcessReadService readService, TimeProvider timeProvider)
{
    public async Task<IssuanceProcessDetails?> HandleAsync(GetIssuanceProcessQuery query, CancellationToken cancellationToken)
    {
        var process = await readService.GetByIdAsync(query.ProcessId, cancellationToken);
        return process?.ToDetails(timeProvider.GetUtcNow());
    }
}

public enum IssuanceTransitionKind { AwaitingStock, Completed, Rejected, ManualIntervention }

public sealed record TransitionInvoiceIssuanceCommand(
    Guid ProcessId, Guid InvoiceId, IssuanceTransitionKind Kind,
    string? OutcomeCode = null, string? OutcomeDescription = null);

public sealed class TransitionInvoiceIssuanceHandler(
    IBillingUnitOfWorkFactory unitOfWorkFactory,
    TimeProvider timeProvider,
    IBillingTelemetry telemetry)
{
    public async Task HandleAsync(TransitionInvoiceIssuanceCommand command, CancellationToken cancellationToken)
    {
        await using var unit = await unitOfWorkFactory.CreateAsync(cancellationToken);
        var process = await unit.GetProcessByIdAsync(command.ProcessId, cancellationToken)
            ?? throw new BillingConsistencyException("Issuance process was not found.");
        if (process.InvoiceId != command.InvoiceId)
            throw new BillingConsistencyException("Issuance process and invoice do not match.");
        var invoice = await unit.GetInvoiceAsync(command.InvoiceId, cancellationToken)
            ?? throw new BillingConsistencyException("Issuance invoice was not found.");

        if (IsEquivalentTerminal(process.Status, command.Kind)) return;
        if (process.Status is InvoiceIssuanceProcessStatus.Completed
            or InvoiceIssuanceProcessStatus.Rejected
            or InvoiceIssuanceProcessStatus.ManualIntervention)
            throw new BillingConsistencyException("A terminal issuance result is incompatible with the received transition.");

        var now = timeProvider.GetUtcNow();
        switch (command.Kind)
        {
            case IssuanceTransitionKind.AwaitingStock:
                process.MarkAwaitingStock(now);
                break;
            case IssuanceTransitionKind.Completed:
                invoice.CompleteIssuance(now);
                process.Complete(now);
                break;
            case IssuanceTransitionKind.Rejected:
                invoice.RejectIssuance(now);
                process.Reject(command.OutcomeCode!, command.OutcomeDescription, now);
                break;
            case IssuanceTransitionKind.ManualIntervention:
                invoice.KeepBlockedForManualIntervention(now);
                process.RequireManualIntervention(command.OutcomeCode!, command.OutcomeDescription, now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }

        await unit.CommitAsync(cancellationToken);
        telemetry.IssuanceTransitioned(process.Status.ToString());
    }

    private static bool IsEquivalentTerminal(InvoiceIssuanceProcessStatus status, IssuanceTransitionKind kind) =>
        (status, kind) is
            (InvoiceIssuanceProcessStatus.Completed, IssuanceTransitionKind.Completed)
            or (InvoiceIssuanceProcessStatus.Rejected, IssuanceTransitionKind.Rejected)
            or (InvoiceIssuanceProcessStatus.ManualIntervention, IssuanceTransitionKind.ManualIntervention);
}
