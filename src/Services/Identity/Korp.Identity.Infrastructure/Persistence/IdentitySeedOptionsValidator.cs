using System.Net.Mail;

namespace Korp.Identity.Infrastructure.Persistence;

public static class IdentitySeedOptionsValidator
{
    public static void EnsureValid(IdentitySeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var email = options.Email?.Trim() ?? string.Empty;
        if (email.Length == 0 || email.Length > 254 || !IsValidEmail(email))
        {
            throw new InvalidOperationException("Identity seed configuration is invalid.");
        }

        var password = options.Password ?? string.Empty;
        if (password.Length is < 12 or > 128
            || !password.Any(char.IsUpper)
            || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit)
            || !password.Any(character => !char.IsLetterOrDigit(character)))
        {
            throw new InvalidOperationException("Identity seed configuration is invalid.");
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address.Equals(email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
