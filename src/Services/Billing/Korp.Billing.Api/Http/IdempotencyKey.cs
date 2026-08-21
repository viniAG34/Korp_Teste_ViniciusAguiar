namespace Korp.Billing.Api.Http;

public readonly record struct IdempotencyKey(Guid Value)
{
    public override string ToString() => Value.ToString("D");

    public static IdempotencyKeyParseResult Parse(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return new IdempotencyKeyParseResult(IdempotencyKeyParseStatus.Missing, null);
        }

        if (!Guid.TryParseExact(headerValue, "D", out var value) || value == Guid.Empty)
        {
            return new IdempotencyKeyParseResult(IdempotencyKeyParseStatus.Invalid, null);
        }

        return new IdempotencyKeyParseResult(
            IdempotencyKeyParseStatus.Valid,
            new IdempotencyKey(value));
    }
}

public readonly record struct IdempotencyKeyParseResult(
    IdempotencyKeyParseStatus Status,
    IdempotencyKey? Value);

public enum IdempotencyKeyParseStatus
{
    Missing,
    Invalid,
    Valid
}
