using Korp.Billing.Api.Http;

namespace Korp.Billing.IntegrationTests.Contracts;

public sealed class HttpPrimitiveTests
{
    [Fact]
    public void EntityTagRoundTripsAnOpaqueVersion()
    {
        var header = new EntityTag(42).ToHeaderValue();

        var result = EntityTag.Parse(header);

        Assert.Equal(EntityTagParseStatus.Valid, result.Status);
        Assert.Equal((uint)42, result.Value?.Version);
        Assert.DoesNotContain("42", header, StringComparison.Ordinal);
        Assert.DoesNotContain('=', header);
    }

    [Theory]
    [InlineData(null, EntityTagParseStatus.Missing)]
    [InlineData("", EntityTagParseStatus.Missing)]
    [InlineData("opaque", EntityTagParseStatus.Invalid)]
    [InlineData("W/\"AAAAKg==\"", EntityTagParseStatus.Invalid)]
    [InlineData("\"AAAAKg==\"", EntityTagParseStatus.Invalid)]
    [InlineData("\"AAAAKg\", \"AAAAKw\"", EntityTagParseStatus.Invalid)]
    [InlineData("\"invalid\"", EntityTagParseStatus.Invalid)]
    public void EntityTagDistinguishesMissingAndInvalidValues(string? value, EntityTagParseStatus expected)
    {
        Assert.Equal(expected, EntityTag.Parse(value).Status);
    }

    [Fact]
    public void IdempotencyKeyAcceptsCanonicalNonEmptyUuid()
    {
        var expected = Guid.NewGuid();

        var result = IdempotencyKey.Parse(expected.ToString("D"));

        Assert.Equal(IdempotencyKeyParseStatus.Valid, result.Status);
        Assert.Equal(expected, result.Value?.Value);
        Assert.Equal(expected.ToString("D"), result.Value?.ToString());
    }

    [Theory]
    [InlineData(null, IdempotencyKeyParseStatus.Missing)]
    [InlineData("", IdempotencyKeyParseStatus.Missing)]
    [InlineData("not-a-uuid", IdempotencyKeyParseStatus.Invalid)]
    [InlineData("00000000-0000-0000-0000-000000000000", IdempotencyKeyParseStatus.Invalid)]
    [InlineData("{9c427743-c807-47bf-91af-46f80401d66b}", IdempotencyKeyParseStatus.Invalid)]
    public void IdempotencyKeyRejectsMissingOrNonCanonicalValues(
        string? value,
        IdempotencyKeyParseStatus expected)
    {
        Assert.Equal(expected, IdempotencyKey.Parse(value).Status);
    }
}
