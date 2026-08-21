namespace Korp.Billing.Domain.Issuance;

public sealed class InvoiceIssuanceProcess
{
    public const int OutcomeCodeMaxLength = 100;
    public const int OutcomeDescriptionMaxLength = 500;

    private InvoiceIssuanceProcess()
    {
    }

    private InvoiceIssuanceProcess(
        Guid id,
        Guid invoiceId,
        Guid idempotencyKey,
        Guid requestedByUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        InvoiceId = invoiceId;
        IdempotencyKey = idempotencyKey;
        RequestedByUserId = requestedByUserId;
        Status = InvoiceIssuanceProcessStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid InvoiceId { get; private set; }

    public Guid IdempotencyKey { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public InvoiceIssuanceProcessStatus Status { get; private set; }

    public string? OutcomeCode { get; private set; }

    public string? OutcomeDescription { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public static InvoiceIssuanceProcess Create(
        Guid id,
        Guid invoiceId,
        Guid idempotencyKey,
        Guid requestedByUserId,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || invoiceId == Guid.Empty || idempotencyKey == Guid.Empty || requestedByUserId == Guid.Empty)
        {
            throw new DomainRuleException(IssuanceErrors.InvalidId, "Issuance identifiers are required.");
        }

        if (createdAtUtc == default)
        {
            throw new DomainRuleException(IssuanceErrors.InvalidTimestamp, "Issuance timestamp is required.");
        }

        return new InvoiceIssuanceProcess(id, invoiceId, idempotencyKey, requestedByUserId, createdAtUtc);
    }

    public void MarkAwaitingStock(DateTimeOffset updatedAtUtc)
    {
        EnsureStatus(InvoiceIssuanceProcessStatus.Pending);
        MoveTo(InvoiceIssuanceProcessStatus.AwaitingStock, updatedAtUtc, null, null, false);
    }

    public void Complete(DateTimeOffset finishedAtUtc)
    {
        EnsureActive();
        MoveTo(InvoiceIssuanceProcessStatus.Completed, finishedAtUtc, null, null, true);
    }

    public void Reject(string outcomeCode, string? outcomeDescription, DateTimeOffset finishedAtUtc)
    {
        EnsureActive();
        MoveTo(InvoiceIssuanceProcessStatus.Rejected, finishedAtUtc, outcomeCode, outcomeDescription, true);
    }

    public void RequireManualIntervention(string outcomeCode, string? outcomeDescription, DateTimeOffset finishedAtUtc)
    {
        EnsureActive();
        MoveTo(InvoiceIssuanceProcessStatus.ManualIntervention, finishedAtUtc, outcomeCode, outcomeDescription, true);
    }

    private void EnsureActive()
    {
        if (Status is not (InvoiceIssuanceProcessStatus.Pending or InvoiceIssuanceProcessStatus.AwaitingStock))
        {
            throw new DomainRuleException(IssuanceErrors.InvalidTransition, "A terminal issuance process cannot transition.");
        }
    }

    private void EnsureStatus(InvoiceIssuanceProcessStatus expectedStatus)
    {
        if (Status != expectedStatus)
        {
            throw new DomainRuleException(IssuanceErrors.InvalidTransition, "Issuance process transition is invalid.");
        }
    }

    private void MoveTo(
        InvoiceIssuanceProcessStatus status,
        DateTimeOffset updatedAtUtc,
        string? outcomeCode,
        string? outcomeDescription,
        bool terminal)
    {
        if (updatedAtUtc < CreatedAtUtc || updatedAtUtc < UpdatedAtUtc)
        {
            throw new DomainRuleException(IssuanceErrors.InvalidTimestamp, "Issuance timestamp cannot move backwards.");
        }

        OutcomeCode = NormalizeOutcomeCode(outcomeCode, status);
        OutcomeDescription = NormalizeOutcomeDescription(outcomeDescription);
        Status = status;
        UpdatedAtUtc = updatedAtUtc;
        FinishedAtUtc = terminal ? updatedAtUtc : null;
    }

    private static string? NormalizeOutcomeCode(string? outcomeCode, InvoiceIssuanceProcessStatus status)
    {
        var requiresCode = status is InvoiceIssuanceProcessStatus.Rejected or InvoiceIssuanceProcessStatus.ManualIntervention;
        if (requiresCode && string.IsNullOrWhiteSpace(outcomeCode))
        {
            throw new DomainRuleException(IssuanceErrors.OutcomeCodeRequired, "Outcome code is required for this status.");
        }

        if (!requiresCode)
        {
            return null;
        }

        var normalizedCode = outcomeCode!.Trim();
        if (normalizedCode.Length > OutcomeCodeMaxLength)
        {
            throw new DomainRuleException(IssuanceErrors.OutcomeCodeTooLong, "Outcome code exceeds its maximum length.");
        }

        return normalizedCode;
    }

    private static string? NormalizeOutcomeDescription(string? outcomeDescription)
    {
        if (string.IsNullOrWhiteSpace(outcomeDescription))
        {
            return null;
        }

        var normalizedDescription = outcomeDescription.Trim();
        if (normalizedDescription.Length > OutcomeDescriptionMaxLength)
        {
            throw new DomainRuleException(IssuanceErrors.OutcomeDescriptionTooLong, "Outcome description exceeds its maximum length.");
        }

        return normalizedDescription;
    }
}
