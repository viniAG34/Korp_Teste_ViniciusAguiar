using System.Buffers.Binary;

namespace Korp.Billing.Api.Http;

public readonly record struct EntityTag(uint Version)
{
    public string ToHeaderValue()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, Version);
        return $"\"{Convert.ToBase64String(bytes)}\"";
    }

    public static EntityTagParseResult Parse(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return new EntityTagParseResult(EntityTagParseStatus.Missing, null);
        }

        if (headerValue.Length < 3 || headerValue[0] != '"' || headerValue[^1] != '"')
        {
            return new EntityTagParseResult(EntityTagParseStatus.Invalid, null);
        }

        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        if (!Convert.TryFromBase64String(headerValue[1..^1], bytes, out var bytesWritten)
            || bytesWritten != sizeof(uint))
        {
            return new EntityTagParseResult(EntityTagParseStatus.Invalid, null);
        }

        var value = new EntityTag(BinaryPrimitives.ReadUInt32BigEndian(bytes));
        return new EntityTagParseResult(EntityTagParseStatus.Valid, value);
    }
}

public readonly record struct EntityTagParseResult(EntityTagParseStatus Status, EntityTag? Value);

public enum EntityTagParseStatus
{
    Missing,
    Invalid,
    Valid
}
