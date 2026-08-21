namespace Korp.Billing.Api.Features.Issuance.Contracts;

public enum InvoiceIssuanceProcessStatusResponse
{
    Pending,
    AwaitingStock,
    Completed,
    Rejected,
    ManualIntervention
}
