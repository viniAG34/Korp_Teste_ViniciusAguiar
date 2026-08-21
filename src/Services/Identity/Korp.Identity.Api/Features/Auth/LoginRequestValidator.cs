using System.Net.Mail;
using Korp.Identity.Api.Features.Auth.Contracts;

namespace Korp.Identity.Api.Features.Auth;

public static class LoginRequestValidator
{
    public static IReadOnlyDictionary<string, string[]> Validate(LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var email = request.Email?.Trim() ?? string.Empty;

        if (email.Length == 0 || email.Length > 254 || !IsValidEmail(email))
        {
            errors["email"] = ["Informe um e-mail válido com no máximo 254 caracteres."];
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
        {
            errors["password"] = ["Informe uma senha com no máximo 128 caracteres."];
        }

        return errors;
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
