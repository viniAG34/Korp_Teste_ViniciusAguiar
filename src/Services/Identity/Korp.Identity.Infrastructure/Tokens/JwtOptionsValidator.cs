using Microsoft.Extensions.Options;

namespace Korp.Identity.Infrastructure.Tokens;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            errors.Add("JWT issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            errors.Add("JWT audience is required.");
        }

        if (!TryDecodeSigningKey(options.SigningKey, out _))
        {
            errors.Add("JWT signing configuration is invalid.");
        }

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    public static bool TryDecodeSigningKey(string value, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            key = Convert.FromBase64String(value);
            return key.Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
