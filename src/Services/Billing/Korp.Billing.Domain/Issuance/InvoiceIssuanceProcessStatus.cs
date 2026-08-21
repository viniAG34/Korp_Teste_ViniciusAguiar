namespace Korp.Billing.Domain.Issuance;

public enum InvoiceIssuanceProcessStatus
{
    Pending = 1,
    AwaitingStock = 2,
    Completed = 3,
    Rejected = 4,
    ManualIntervention = 5,
}
