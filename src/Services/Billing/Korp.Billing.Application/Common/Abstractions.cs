namespace Korp.Billing.Application.Common;

public interface IGuidGenerator
{
    Guid NewGuid();
}

public interface IBillingTelemetry
{
    void InvoiceCreated();
    void ItemOperation(string operation, string outcome);
    void IssuanceRequested(string outcome);
    void IssuanceTransitioned(string status);
    void ProductCatalogRequest(string outcome);
}

public sealed class BillingServiceUnavailableException(Exception? innerException = null)
    : Exception("Billing persistence is unavailable.", innerException);

public sealed class BillingConcurrencyException(Exception? innerException = null)
    : Exception("Billing state changed concurrently.", innerException);

public sealed class BillingConsistencyException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class BillingMessageIntegrityException()
    : Exception("A message identifier was reused with different content.");

public sealed class BillingResultContradictionException()
    : Exception("The received result contradicts the terminal issuance state.");

public enum BillingConstraint
{
    ProductAlreadyAdded,
    IdempotencyKey,
    ActiveIssuance
}

public sealed class BillingConstraintException(BillingConstraint constraint, Exception innerException)
    : Exception("A known Billing constraint was violated.", innerException)
{
    public BillingConstraint Constraint { get; } = constraint;
}
