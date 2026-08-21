using Korp.Billing.Application.Common;
using Korp.Billing.Domain;
using Korp.Billing.Domain.Invoices;

namespace Korp.Billing.Application.Invoices;

public sealed record CreateInvoiceCommand(Guid CreatedByUserId);
public sealed record GetInvoiceByIdQuery(Guid InvoiceId);
public sealed record ListInvoicesQuery(int PageNumber, int PageSize);
public sealed record AddInvoiceItemCommand(Guid InvoiceId, Guid ProductId, int Quantity, uint ExpectedVersion);
public sealed record UpdateInvoiceItemQuantityCommand(Guid InvoiceId, Guid ItemId, int Quantity, uint ExpectedVersion);
public sealed record RemoveInvoiceItemCommand(Guid InvoiceId, Guid ItemId, uint ExpectedVersion);

public enum InvoiceMutationStatus
{
    Success,
    InvoiceNotFound,
    ItemNotFound,
    ProductNotFound,
    ProductAlreadyAdded,
    InvoiceNotOpen,
    IssuanceInProgress,
    VersionMismatch
}

public sealed record InvoiceMutationResult(InvoiceMutationStatus Status, InvoiceDetails? Invoice = null)
{
    public static InvoiceMutationResult Success(Invoice invoice) => new(InvoiceMutationStatus.Success, invoice.ToDetails());
    public static InvoiceMutationResult Failure(InvoiceMutationStatus status) => new(status);
}

public sealed class CreateInvoiceHandler(
    IInvoiceRepository repository,
    IInvoiceNumberGenerator numberGenerator,
    IGuidGenerator guidGenerator,
    TimeProvider timeProvider,
    IBillingTelemetry telemetry)
{
    public async Task<InvoiceDetails> HandleAsync(CreateInvoiceCommand command, CancellationToken cancellationToken)
    {
        var number = await numberGenerator.GetNextAsync(cancellationToken);
        var invoice = Invoice.Create(guidGenerator.NewGuid(), number, command.CreatedByUserId, timeProvider.GetUtcNow());
        repository.Add(invoice);
        await repository.SaveChangesAsync(cancellationToken);
        telemetry.InvoiceCreated();
        return invoice.ToDetails();
    }
}

public sealed class GetInvoiceByIdHandler(IInvoiceReadService readService)
{
    public Task<InvoiceDetails?> HandleAsync(GetInvoiceByIdQuery query, CancellationToken cancellationToken) =>
        readService.GetByIdAsync(query.InvoiceId, cancellationToken);
}

public sealed class ListInvoicesHandler(IInvoiceReadService readService)
{
    public Task<InvoicePage> HandleAsync(ListInvoicesQuery query, CancellationToken cancellationToken) =>
        readService.ListAsync(query.PageNumber, query.PageSize, cancellationToken);
}

public sealed class AddInvoiceItemHandler(
    IInvoiceRepository repository,
    IProductCatalogClient productCatalog,
    IGuidGenerator guidGenerator,
    TimeProvider timeProvider,
    IBillingTelemetry telemetry)
{
    public async Task<InvoiceMutationResult> HandleAsync(AddInvoiceItemCommand command, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(command.InvoiceId, cancellationToken);
        var local = Validate(invoice, command.ExpectedVersion);
        if (local is not null) return local;
        if (invoice!.Items.Any(item => item.ProductId == command.ProductId))
            return InvoiceMutationResult.Failure(InvoiceMutationStatus.ProductAlreadyAdded);

        var snapshot = await productCatalog.GetSnapshotAsync(command.ProductId, cancellationToken);
        if (snapshot is null) return InvoiceMutationResult.Failure(InvoiceMutationStatus.ProductNotFound);

        try
        {
            invoice.AddItem(guidGenerator.NewGuid(), snapshot.Id, snapshot.Code, snapshot.Description,
                command.Quantity, timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
            telemetry.ItemOperation("add", "success");
            return InvoiceMutationResult.Success(invoice);
        }
        catch (BillingConcurrencyException)
        {
            return InvoiceMutationResult.Failure(InvoiceMutationStatus.VersionMismatch);
        }
        catch (BillingConstraintException exception) when (exception.Constraint == BillingConstraint.ProductAlreadyAdded)
        {
            return InvoiceMutationResult.Failure(InvoiceMutationStatus.ProductAlreadyAdded);
        }
    }

    private static InvoiceMutationResult? Validate(Invoice? invoice, uint expectedVersion)
    {
        if (invoice is null) return InvoiceMutationResult.Failure(InvoiceMutationStatus.InvoiceNotFound);
        if (invoice.Version != expectedVersion) return InvoiceMutationResult.Failure(InvoiceMutationStatus.VersionMismatch);
        if (invoice.Status != InvoiceStatus.Open) return InvoiceMutationResult.Failure(InvoiceMutationStatus.InvoiceNotOpen);
        if (invoice.IsIssuanceInProgress) return InvoiceMutationResult.Failure(InvoiceMutationStatus.IssuanceInProgress);
        return null;
    }
}

public sealed class UpdateInvoiceItemQuantityHandler(
    IInvoiceRepository repository, TimeProvider timeProvider, IBillingTelemetry telemetry)
{
    public async Task<InvoiceMutationResult> HandleAsync(UpdateInvoiceItemQuantityCommand command, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(command.InvoiceId, cancellationToken);
        var validation = Validate(invoice, command.ExpectedVersion, command.ItemId);
        if (validation is not null) return validation;
        invoice!.UpdateItemQuantity(command.ItemId, command.Quantity, timeProvider.GetUtcNow());
        try { await repository.SaveChangesAsync(cancellationToken); }
        catch (BillingConcurrencyException) { return InvoiceMutationResult.Failure(InvoiceMutationStatus.VersionMismatch); }
        telemetry.ItemOperation("update", "success");
        return InvoiceMutationResult.Success(invoice);
    }

    internal static InvoiceMutationResult? Validate(Invoice? invoice, uint expectedVersion, Guid itemId)
    {
        if (invoice is null) return InvoiceMutationResult.Failure(InvoiceMutationStatus.InvoiceNotFound);
        if (invoice.Version != expectedVersion) return InvoiceMutationResult.Failure(InvoiceMutationStatus.VersionMismatch);
        if (invoice.Status != InvoiceStatus.Open) return InvoiceMutationResult.Failure(InvoiceMutationStatus.InvoiceNotOpen);
        if (invoice.IsIssuanceInProgress) return InvoiceMutationResult.Failure(InvoiceMutationStatus.IssuanceInProgress);
        if (!invoice.Items.Any(item => item.Id == itemId)) return InvoiceMutationResult.Failure(InvoiceMutationStatus.ItemNotFound);
        return null;
    }
}

public sealed class RemoveInvoiceItemHandler(
    IInvoiceRepository repository, TimeProvider timeProvider, IBillingTelemetry telemetry)
{
    public async Task<InvoiceMutationResult> HandleAsync(RemoveInvoiceItemCommand command, CancellationToken cancellationToken)
    {
        var invoice = await repository.GetByIdAsync(command.InvoiceId, cancellationToken);
        var validation = UpdateInvoiceItemQuantityHandler.Validate(invoice, command.ExpectedVersion, command.ItemId);
        if (validation is not null) return validation;
        invoice!.RemoveItem(command.ItemId, timeProvider.GetUtcNow());
        try { await repository.SaveChangesAsync(cancellationToken); }
        catch (BillingConcurrencyException) { return InvoiceMutationResult.Failure(InvoiceMutationStatus.VersionMismatch); }
        telemetry.ItemOperation("remove", "success");
        return InvoiceMutationResult.Success(invoice);
    }
}
