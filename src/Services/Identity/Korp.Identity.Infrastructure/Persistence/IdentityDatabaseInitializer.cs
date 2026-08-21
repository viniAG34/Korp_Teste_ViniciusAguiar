using Microsoft.AspNetCore.Identity;

namespace Korp.Identity.Infrastructure.Persistence;

public sealed class IdentityDatabaseInitializer(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
{
    public const string AdministratorRole = "Admin";

    public async Task InitializeAsync(IdentitySeedOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Password);
        cancellationToken.ThrowIfCancellationRequested();

        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(AdministratorRole)));
        }

        var normalizedEmail = userManager.NormalizeEmail(options.Email);
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = options.Email.Trim(),
                UserName = options.Email.Trim(),
                EmailConfirmed = true,
            };

            EnsureSucceeded(await userManager.CreateAsync(user, options.Password));
        }

        if (!await userManager.IsInRoleAsync(user, AdministratorRole))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(user, AdministratorRole));
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Identity database initialization failed.");
        }
    }
}
