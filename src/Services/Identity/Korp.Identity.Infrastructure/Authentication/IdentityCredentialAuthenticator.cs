using System.Data.Common;
using Korp.Identity.Application.Authentication;
using Korp.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Korp.Identity.Infrastructure.Authentication;

public sealed class IdentityCredentialAuthenticator(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : ICredentialAuthenticator
{
    public async Task<CredentialAuthenticationResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                _ = userManager.PasswordHasher.HashPassword(new ApplicationUser(), password);
                return CredentialAuthenticationResult.Invalid();
            }

            var signInResult = await signInManager.CheckPasswordSignInAsync(
                user,
                password,
                lockoutOnFailure: true);
            if (!signInResult.Succeeded)
            {
                return CredentialAuthenticationResult.Invalid();
            }

            var roles = (await userManager.GetRolesAsync(user))
                .Order(StringComparer.Ordinal)
                .ToArray();
            cancellationToken.ThrowIfCancellationRequested();

            return CredentialAuthenticationResult.Success(
                new AuthenticatedIdentity(
                    user.Id,
                    user.Email ?? user.UserName ?? email,
                    roles));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsDependencyUnavailable(exception))
        {
            throw new IdentityServiceUnavailableException(exception);
        }
    }

    private static bool IsDependencyUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException or DbUpdateException or TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}
