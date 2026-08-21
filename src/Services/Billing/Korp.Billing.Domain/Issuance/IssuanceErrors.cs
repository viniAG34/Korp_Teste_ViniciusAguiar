namespace Korp.Billing.Domain.Issuance;

public static class IssuanceErrors
{
    public const string InvalidId = "issuance_invalid_id";
    public const string InvalidTimestamp = "issuance_invalid_timestamp";
    public const string InvalidTransition = "issuance_invalid_transition";
    public const string OutcomeCodeRequired = "issuance_outcome_code_required";
    public const string OutcomeCodeTooLong = "issuance_outcome_code_too_long";
    public const string OutcomeDescriptionTooLong = "issuance_outcome_description_too_long";
}
